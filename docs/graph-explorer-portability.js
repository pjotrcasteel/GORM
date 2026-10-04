(()=>{
  const modelStorageKey='gorm.graphExplorer.model.v2';
  const canvasStorageKey='gorm.graphExplorer.canvas.v1';
  const format='gorm-graph-model';
  const version=1;
  const identifier=/^[A-Za-z_][A-Za-z0-9_]*$/;
  const maxNodes=200;
  const maxEdges=500;
  const maxFileBytes=1024*1024;
  const fallbackModel={nodes:[
    {id:'gateway',type:'GatewayNode',label:'Gateway',variable:'gatewayId',description:'Entry point that routes requests to an application service.',x:140,y:280},
    {id:'service',type:'ServiceNode',label:'Order Service',variable:'serviceId',description:'Application service with infrastructure dependencies and published messages.',x:400,y:280},
    {id:'database',type:'DatabaseNode',label:'Orders DB',variable:'databaseId',description:'Persistent store used by the service through a typed dependency.',x:720,y:130},
    {id:'queue',type:'QueueNode',label:'Order Events',variable:'queueId',description:'Message destination used by publishers and consumed by workers.',x:720,y:390},
    {id:'worker',type:'WorkerNode',label:'Billing Worker',variable:'workerId',description:'Background processor that consumes messages from the queue.',x:910,y:390}
  ],edges:[
    {id:'routes',type:'RoutesToEdge',label:'routes to',source:'gateway',target:'service'},
    {id:'depends',type:'DependsOnEdge',label:'depends on',source:'service',target:'database'},
    {id:'publishes',type:'PublishesToEdge',label:'publishes to',source:'service',target:'queue'},
    {id:'consumes',type:'ConsumesFromEdge',label:'consumes from',source:'worker',target:'queue'}
  ]};
  const $=id=>document.getElementById(id);
  const clone=value=>JSON.parse(JSON.stringify(value));
  const status=text=>{const target=$('designer-status');if(target)target.textContent=text;};

  function unique(values){return new Set(values).size===values.length;}
  function validText(value,max=80){return typeof value==='string'&&value.length>0&&value.length<=max;}
  function validCoordinate(value){return value===null||value===undefined||(Number.isFinite(value)&&value>=0&&value<=1200);}
  function validateModel(model){
    if(!model||!Array.isArray(model.nodes)||!Array.isArray(model.edges))return'Model must contain nodes and edges arrays.';
    if(model.nodes.length<1||model.nodes.length>maxNodes)return`Model must contain between 1 and ${maxNodes} nodes.`;
    if(model.edges.length>maxEdges)return`Model cannot contain more than ${maxEdges} edges.`;
    const nodeIds=model.nodes.map(node=>node.id),nodeTypes=model.nodes.map(node=>String(node.type).toLowerCase());
    const edgeIds=model.edges.map(edge=>edge.id),edgeTypes=model.edges.map(edge=>String(edge.type).toLowerCase());
    if(!unique(nodeIds))return'Node IDs must be unique.';
    if(!unique(nodeTypes))return'Node CLR types must be unique.';
    if(!unique(edgeIds))return'Edge IDs must be unique.';
    if(!unique(edgeTypes))return'Edge CLR types must be unique.';
    for(const node of model.nodes){
      if(!validText(node.id)||!validText(node.type)||!validText(node.label)||!validText(node.variable))return'Every node needs a valid id, CLR type, label and Id variable.';
      if(!identifier.test(node.type)||!identifier.test(node.variable))return`Node ${node.label||node.id} contains an invalid C# identifier.`;
      if(node.description!==undefined&&node.description!==null&&(typeof node.description!=='string'||node.description.length>280))return`Node ${node.label} has an invalid description.`;
      if(!validCoordinate(node.x)||!validCoordinate(node.y))return`Node ${node.label} has an invalid canvas position.`;
    }
    const knownNodes=new Set(nodeIds);
    for(const edge of model.edges){
      if(!validText(edge.id)||!validText(edge.type)||!validText(edge.label))return'Every edge needs a valid id, CLR type and label.';
      if(!identifier.test(edge.type))return`Edge ${edge.label||edge.id} contains an invalid C# identifier.`;
      if(!knownNodes.has(edge.source)||!knownNodes.has(edge.target))return`Edge ${edge.type} references an unknown node.`;
      if(edge.source===edge.target)return`Edge ${edge.type} must connect two different nodes.`;
    }
    return null;
  }
  function readJson(key){try{const raw=localStorage.getItem(key);return raw?JSON.parse(raw):null;}catch{return null;}}
  function currentModel(){const stored=readJson(modelStorageKey);return validateModel(stored)?clone(fallbackModel):stored;}
  function currentCanvas(){const stored=readJson(canvasStorageKey);return{locked:Boolean(stored?.locked)};}
  function envelope(){return{format,version,exportedAt:new Date().toISOString(),model:currentModel(),canvas:currentCanvas()};}
  function fileName(){const date=new Date().toISOString().slice(0,10);return`gorm-graph-model-${date}.json`;}
  function downloadJson(){
    const json=JSON.stringify(envelope(),null,2),blob=new Blob([json],{type:'application/json'}),url=URL.createObjectURL(blob),link=document.createElement('a');
    link.href=url;link.download=fileName();document.body.appendChild(link);link.click();link.remove();URL.revokeObjectURL(url);status('Graph model exported as portable JSON.');
  }
  function normalizeImport(value){
    if(value?.format===format){if(value.version!==version)throw new Error(`Unsupported ${format} version ${value.version}. Expected version ${version}.`);return{model:value.model,canvas:value.canvas};}
    if(value?.nodes&&value?.edges)return{model:value,canvas:null};
    throw new Error(`Expected a ${format} export or a graph model with nodes and edges.`);
  }
  async function importFile(file){
    if(!file)return;
    if(file.size>maxFileBytes){status('Import rejected: JSON file is larger than 1 MB.');return;}
    try{
      const parsed=JSON.parse(await file.text()),incoming=normalizeImport(parsed),error=validateModel(incoming.model);
      if(error){status(`Import rejected: ${error}`);return;}
      if(!confirm(`Replace the current graph with ${incoming.model.nodes.length} nodes and ${incoming.model.edges.length} edges from ${file.name}?`))return;
      localStorage.setItem(modelStorageKey,JSON.stringify(incoming.model));
      if(incoming.canvas&&typeof incoming.canvas.locked==='boolean')localStorage.setItem(canvasStorageKey,JSON.stringify({locked:incoming.canvas.locked}));
      else localStorage.removeItem(canvasStorageKey);
      location.reload();
    }catch(error){status(`Import rejected: ${error instanceof Error?error.message:'invalid JSON file.'}`);}
  }
  function install(){
    const actions=document.querySelector('.designer-heading-actions');if(!actions||$('export-model'))return;
    const exportButton=document.createElement('button');exportButton.id='export-model';exportButton.type='button';exportButton.className='tiny-button';exportButton.textContent='Export JSON';exportButton.addEventListener('click',downloadJson);
    const importButton=document.createElement('button');importButton.id='import-model';importButton.type='button';importButton.className='tiny-button';importButton.textContent='Import JSON';
    const input=document.createElement('input');input.type='file';input.accept='application/json,.json';input.hidden=true;input.addEventListener('change',async()=>{const [file]=input.files??[];await importFile(file);input.value='';});
    importButton.addEventListener('click',()=>input.click());actions.append(exportButton,importButton,input);
  }

  install();
})();