(()=>{
  const svgNs='http://www.w3.org/2000/svg';
  const storageKey='gorm.graphExplorer.model.v2';
  const defaultModel={
    nodes:[
      {id:'gateway',type:'GatewayNode',label:'Gateway',description:'Entry point that routes requests to an application service.',variable:'gatewayId',x:140,y:280},
      {id:'service',type:'ServiceNode',label:'Order Service',description:'Application service with infrastructure dependencies and published messages.',variable:'serviceId',x:400,y:280},
      {id:'database',type:'DatabaseNode',label:'Orders DB',description:'Persistent store used by the service through a typed dependency.',variable:'databaseId',x:720,y:130},
      {id:'queue',type:'QueueNode',label:'Order Events',description:'Message destination used by publishers and consumed by workers.',variable:'queueId',x:720,y:390},
      {id:'worker',type:'WorkerNode',label:'Billing Worker',description:'Background processor that consumes messages from the queue.',variable:'workerId',x:910,y:390}
    ],
    edges:[
      {id:'routes',type:'RoutesToEdge',label:'routes to',source:'gateway',target:'service'},
      {id:'depends',type:'DependsOnEdge',label:'depends on',source:'service',target:'database'},
      {id:'publishes',type:'PublishesToEdge',label:'publishes to',source:'service',target:'queue'},
      {id:'consumes',type:'ConsumesFromEdge',label:'consumes from',source:'worker',target:'queue'}
    ]
  };
  const presets={
    'gateway-database':{root:'gateway',edges:['routes','depends']},
    'service-queue':{root:'service',edges:['publishes']},
    'database-dependents':{root:'database',edges:['depends']},
    'queue-consumers':{root:'queue',edges:['consumes']}
  };
  const state={root:null,hops:[]};
  let model=loadModel();
  const canvas=document.getElementById('graph-canvas');
  const svg=document.getElementById('graph-edges');
  const nodesHost=document.getElementById('graph-nodes');
  if(!canvas||!svg||!nodesHost)return;

  const nodeForm=document.getElementById('node-form');
  const edgeForm=document.getElementById('edge-form');
  const status=document.getElementById('designer-status');
  const clone=value=>JSON.parse(JSON.stringify(value));
  const nodeById=id=>model.nodes.find(node=>node.id===id);
  const edgeById=id=>model.edges.find(edge=>edge.id===id);

  function loadModel(){
    try{
      const raw=localStorage.getItem(storageKey);if(!raw)return clone(defaultModel);
      const parsed=JSON.parse(raw);return isValidModel(parsed)?parsed:clone(defaultModel);
    }catch{return clone(defaultModel);}
  }
  function isValidModel(value){
    if(!value||!Array.isArray(value.nodes)||!Array.isArray(value.edges)||!value.nodes.length)return false;
    const ids=new Set(value.nodes.map(node=>node.id));
    return value.nodes.every(node=>node.id&&node.type&&node.label&&node.variable)&&value.edges.every(edge=>edge.id&&edge.type&&ids.has(edge.source)&&ids.has(edge.target));
  }
  function persistModel(){localStorage.setItem(storageKey,JSON.stringify(model));}
  function setStatus(message){status.textContent=message;}
  function resetPath(){state.root=null;state.hops=[];}
  function afterModelChange(message){persistModel();resetPath();renderDesigner();renderModel();renderPath();renderOutput();setStatus(`${message} Traversal path reset.`);}
  function currentNodeId(){return state.hops.at(-1)?.to??state.root;}
  function usedEdgeIds(){return new Set(state.hops.map(hop=>hop.edge));}
  function availableEdges(){
    const current=currentNodeId();if(!current)return [];
    const used=usedEdgeIds();
    return model.edges.filter(edge=>!used.has(edge.id)&&(edge.source===current||edge.target===current));
  }
  function relationFor(edge,current=currentNodeId()){
    if(edge.source===current)return{direction:'outgoing',to:edge.target};
    if(edge.target===current)return{direction:'incoming',to:edge.source};
    return null;
  }

  function createSvg(name,attributes={}){const element=document.createElementNS(svgNs,name);Object.entries(attributes).forEach(([key,value])=>element.setAttribute(key,String(value)));return element;}
  function addMarkers(){
    const defs=createSvg('defs');
    [['arrow-default','#4a4552'],['arrow-selectable','#b797ff'],['arrow-selected','#9fe3d5']].forEach(([id,color])=>{
      const marker=createSvg('marker',{id,viewBox:'0 0 10 10',refX:'8.5',refY:'5',markerWidth:'7',markerHeight:'7',orient:'auto-start-reverse'});
      marker.appendChild(createSvg('path',{d:'M 0 0 L 10 5 L 0 10 z',fill:color}));defs.appendChild(marker);
    });
    svg.appendChild(defs);
  }
  function layoutNodes(){
    if(model.nodes.length<=5&&model.nodes.every(node=>Number.isFinite(node.x)&&Number.isFinite(node.y)))return;
    const columns=model.nodes.length<=4?2:model.nodes.length<=9?3:4;
    const rows=Math.ceil(model.nodes.length/columns);
    model.nodes.forEach((node,index)=>{
      const column=index%columns,row=Math.floor(index/columns);node.x=120+(760*(column/(Math.max(1,columns-1))));node.y=95+(370*(row/(Math.max(1,rows-1))));
    });
  }
  function shortenedLine(source,target,padding=82){
    const dx=target.x-source.x,dy=target.y-source.y,length=Math.hypot(dx,dy)||1,ux=dx/length,uy=dy/length;
    return{x1:source.x+ux*padding,y1:source.y+uy*padding,x2:target.x-ux*padding,y2:target.y-uy*padding};
  }
  function renderModel(){
    layoutNodes();svg.textContent='';addMarkers();nodesHost.textContent='';
    model.edges.forEach(edge=>{
      const source=nodeById(edge.source),target=nodeById(edge.target);if(!source||!target)return;const line=shortenedLine(source,target);
      const group=createSvg('g',{class:'graph-edge-group','data-edge':edge.id,tabindex:'0',role:'button','aria-label':`${edge.type}: ${source.type} to ${target.type}`});
      const hit=createSvg('line',{class:'graph-edge-hit',...line});
      const visible=createSvg('line',{class:'graph-edge-line',...line,'marker-end':'url(#arrow-default)'});
      const mx=(line.x1+line.x2)/2,my=(line.y1+line.y2)/2;
      const width=Math.max(116,edge.type.length*7.3+24);
      const bg=createSvg('rect',{class:'graph-edge-label-bg',x:mx-width/2,y:my-15,width,height:30,rx:10});
      const label=createSvg('text',{class:'graph-edge-label',x:mx,y:my});label.textContent=edge.type;
      group.append(hit,visible,bg,label);group.addEventListener('click',()=>extendByEdge(edge.id));group.addEventListener('keydown',event=>{if(event.key==='Enter'||event.key===' '){event.preventDefault();extendByEdge(edge.id);}});svg.appendChild(group);
    });
    model.nodes.forEach(node=>{
      const button=document.createElement('button');button.type='button';button.className='graph-node-button';button.dataset.node=node.id;button.style.left=`${node.x/10}%`;button.style.top=`${node.y/5.6}%`;
      const kind=document.createElement('small');kind.textContent=node.type;const label=document.createElement('strong');label.textContent=node.label;button.append(kind,label);
      button.addEventListener('click',()=>setRoot(node.id));nodesHost.appendChild(button);
    });
    updateGraphState();
  }

  function setRoot(nodeId){if(!nodeById(nodeId))return;state.root=nodeId;state.hops=[];renderAll();}
  function extendByEdge(edgeId){
    const edge=edgeById(edgeId);const relation=edge?relationFor(edge):null;if(!relation||usedEdgeIds().has(edgeId))return;
    state.hops.push({edge:edgeId,direction:relation.direction,from:currentNodeId(),to:relation.to});renderAll();
  }
  function applyPreset(key){
    const preset=presets[key];if(!preset||!nodeById(preset.root)||preset.edges.some(id=>!edgeById(id))){setStatus('That demo preset is not available in the current custom model. Restore the demo model to use it.');return;}
    state.root=preset.root;state.hops=[];
    preset.edges.forEach(edgeId=>{const edge=edgeById(edgeId);const relation=edge?relationFor(edge):null;if(relation)state.hops.push({edge:edgeId,direction:relation.direction,from:currentNodeId(),to:relation.to});});
    renderAll();
  }

  function updateGraphState(){
    const current=currentNodeId(),available=new Set(availableEdges().map(edge=>edge.id)),selected=new Set(state.hops.map(hop=>hop.edge));
    document.querySelectorAll('.graph-node-button').forEach(button=>{button.classList.toggle('root',button.dataset.node===state.root);button.classList.toggle('current',button.dataset.node===current);});
    svg.querySelectorAll('.graph-edge-group').forEach(group=>{
      const id=group.getAttribute('data-edge'),isSelected=selected.has(id),isAvailable=available.has(id);group.classList.toggle('selected',isSelected);group.classList.toggle('selectable',!isSelected&&isAvailable);group.classList.toggle('dim',Boolean(current)&&!isSelected&&!isAvailable);
      const line=group.querySelector('.graph-edge-line');if(line)line.setAttribute('marker-end',isSelected?'url(#arrow-selected)':isAvailable?'url(#arrow-selectable)':'url(#arrow-default)');
      group.setAttribute('aria-disabled',String(Boolean(current)&&!isSelected&&!isAvailable));
    });
  }

  function pathText(){
    if(!state.root)return'';const root=nodeById(state.root);if(!root)return'';let text=root.type;
    state.hops.forEach(hop=>{const edge=edgeById(hop.edge),target=nodeById(hop.to);if(!edge||!target)return;text+=hop.direction==='outgoing'?` -[${edge.type}]-> ${target.type}`:` <-[${edge.type}]- ${target.type}`;});return text;
  }
  function renderPath(){
    const flow=document.getElementById('path-flow'),title=document.getElementById('path-title'),details=document.getElementById('current-node');flow.textContent='';details.textContent='';
    const root=nodeById(state.root);if(!root){const empty=document.createElement('p');empty.textContent='Select any node in the model to begin.';flow.appendChild(empty);title.textContent='Choose a root node';return;}
    const current=nodeById(currentNodeId());title.textContent=state.hops.length?`${state.hops.length} hop${state.hops.length===1?'':'s'} · ${current.type}`:`Root · ${root.type}`;
    appendChip(flow,'ROOT',root.type,'node');
    state.hops.forEach(hop=>{const edge=edgeById(hop.edge),target=nodeById(hop.to);if(!edge||!target)return;const arrow=document.createElement('span');arrow.className='path-arrow';arrow.textContent=hop.direction==='outgoing'?'→':'←';flow.appendChild(arrow);appendChip(flow,hop.direction.toUpperCase(),edge.type,`edge ${hop.direction}`);const next=document.createElement('span');next.className='path-arrow';next.textContent='→';flow.appendChild(next);appendChip(flow,'NODE',target.type,'node');});
    const kicker=document.createElement('span');kicker.textContent='CURRENT NODE';const heading=document.createElement('h3');heading.textContent=current.label;const paragraph=document.createElement('p');paragraph.textContent=current.description||`${current.type} in the current documentation model.`;details.append(kicker,heading,paragraph);
    const relations=availableEdges();if(relations.length){const list=document.createElement('div');list.className='available-list';relations.forEach(edge=>{const relation=relationFor(edge),target=nodeById(relation.to);const button=document.createElement('button');button.type='button';const text=document.createElement('span');text.textContent=`${relation.direction==='outgoing'?'Outgoing':'Incoming'} ${edge.type}`;const small=document.createElement('small');small.textContent=target.type;button.append(text,small);button.addEventListener('click',()=>extendByEdge(edge.id));list.appendChild(button);});details.appendChild(list);}else{const done=document.createElement('p');done.textContent='No unused relationships continue from this node.';details.appendChild(done);}
  }
  function appendChip(host,label,value,className){const chip=document.createElement('div');chip.className=`path-chip ${className}`;const small=document.createElement('small');small.textContent=label;const strong=document.createElement('strong');strong.textContent=value;chip.append(small,strong);host.appendChild(chip);}

  function buildQuery(){
    const root=nodeById(state.root);if(!root)return'// Choose a root node to generate a traversal.';const lines=[`var result = await context.Set<${root.type}>()`,`    .Where(x => x.Id == ${root.variable})`];
    state.hops.forEach(hop=>{const edge=edgeById(hop.edge),target=nodeById(hop.to);if(!edge||!target)return;const method=hop.direction==='outgoing'?'Outgoing':'Incoming';lines.push(`    .${method}<${edge.type}, ${target.type}>()`);});lines.push('    .ToListAsync();');return lines.join('\n');
  }
  function buildSql(){
    const root=nodeById(state.root);if(!root)return'-- Choose a root node to generate the SQL shape.';const tables=[`[${root.type}] AS node0`],matches=[];
    state.hops.forEach((hop,index)=>{const edge=edgeById(hop.edge),target=nodeById(hop.to);if(!edge||!target)return;tables.push(`[${edge.type}] AS edge${index}`,`[${target.type}] AS node${index+1}`);matches.push(hop.direction==='outgoing'?`MATCH(node${index}-(edge${index})->node${index+1})`:`MATCH(node${index+1}-(edge${index})->node${index})`);});
    const resultAlias=`node${state.hops.length}`;let sql=`-- Documentation query shape\nSELECT ${resultAlias}.*\nFROM ${tables.join(',\n     ')}\nWHERE `;if(matches.length)sql+=`${matches.join('\n  AND ')}\n  AND `;sql+=`node0.[Id] = @${root.variable};`;return sql;
  }
  function encodeQuery(value){const bytes=new TextEncoder().encode(value);let binary='';bytes.forEach(byte=>binary+=String.fromCharCode(byte));return btoa(binary).replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');}
  function playgroundUrl(query){const url=new URL('./playground.html',location.href);url.searchParams.set('q',encodeQuery(query));return url.href;}
  function renderOutput(){
    const query=buildQuery(),sql=buildSql(),hasRoot=Boolean(nodeById(state.root));document.getElementById('gorm-query').textContent=query;document.getElementById('graph-sql').textContent=sql;
    ['copy-query','copy-explorer-sql','copy-path'].forEach(id=>{document.getElementById(id).disabled=!hasRoot;});document.getElementById('undo-hop').disabled=!state.hops.length;
    const link=document.getElementById('open-playground');link.href=hasRoot?playgroundUrl(query):'./playground.html';link.classList.toggle('disabled',!hasRoot);link.setAttribute('aria-disabled',String(!hasRoot));
  }
  function renderAll(){updateGraphState();renderPath();renderOutput();}

  function slug(value,fallback){const result=value.trim().replace(/([a-z0-9])([A-Z])/g,'$1-$2').replace(/[^A-Za-z0-9]+/g,'-').replace(/^-|-$/g,'').toLowerCase();return result||fallback;}
  function uniqueId(base,items,currentId=''){let candidate=base,index=2;while(items.some(item=>item.id===candidate&&item.id!==currentId))candidate=`${base}-${index++}`;return candidate;}
  function validIdentifier(value){return /^[A-Za-z_][A-Za-z0-9_]*$/.test(value);}
  function duplicateType(type,items,currentId){return items.some(item=>item.id!==currentId&&item.type.toLowerCase()===type.toLowerCase());}
  function showForm(form){form.hidden=false;form.scrollIntoView({behavior:'smooth',block:'nearest'});}
  function hideForm(form){form.hidden=true;}
  function populateNodeSelects(){
    ['edge-source','edge-target'].forEach(id=>{const select=document.getElementById(id),selected=select.value;select.textContent='';model.nodes.forEach(node=>{const option=document.createElement('option');option.value=node.id;option.textContent=`${node.label} · ${node.type}`;select.appendChild(option);});if(model.nodes.some(node=>node.id===selected))select.value=selected;});
  }
  function renderDesigner(){
    document.getElementById('node-count').textContent=`${model.nodes.length} node${model.nodes.length===1?'':'s'}`;document.getElementById('edge-count').textContent=`${model.edges.length} edge${model.edges.length===1?'':'s'}`;
    const nodeList=document.getElementById('node-list');nodeList.textContent='';model.nodes.forEach(node=>{const item=document.createElement('button');item.type='button';item.className='model-item';const body=document.createElement('span');const strong=document.createElement('strong');strong.textContent=node.label;const meta=document.createElement('small');meta.textContent=`${node.type} · ${node.variable}`;body.append(strong,meta);const edit=document.createElement('b');edit.textContent='Edit';item.append(body,edit);item.addEventListener('click',()=>openNodeForm(node.id));nodeList.appendChild(item);});
    const edgeList=document.getElementById('edge-list');edgeList.textContent='';model.edges.forEach(edge=>{const source=nodeById(edge.source),target=nodeById(edge.target);const item=document.createElement('button');item.type='button';item.className='model-item';const body=document.createElement('span');const strong=document.createElement('strong');strong.textContent=edge.type;const meta=document.createElement('small');meta.textContent=`${source?.label??edge.source} → ${target?.label??edge.target}`;body.append(strong,meta);const edit=document.createElement('b');edit.textContent='Edit';item.append(body,edit);item.addEventListener('click',()=>openEdgeForm(edge.id));edgeList.appendChild(item);});
    populateNodeSelects();document.getElementById('new-edge').disabled=model.nodes.length<2;
  }
  function openNodeForm(id=''){
    const node=nodeById(id);document.getElementById('node-form-title').textContent=node?'Edit node':'Add node';document.getElementById('node-id').value=node?.id??'';document.getElementById('node-type').value=node?.type??'';document.getElementById('node-label').value=node?.label??'';document.getElementById('node-variable').value=node?.variable??'';document.getElementById('node-description').value=node?.description??'';document.getElementById('delete-node').hidden=!node;showForm(nodeForm);document.getElementById('node-type').focus();
  }
  function openEdgeForm(id=''){
    if(model.nodes.length<2){setStatus('Add at least two nodes before creating an edge.');return;}const edge=edgeById(id);populateNodeSelects();document.getElementById('edge-form-title').textContent=edge?'Edit edge':'Add edge';document.getElementById('edge-id').value=edge?.id??'';document.getElementById('edge-type').value=edge?.type??'';document.getElementById('edge-label').value=edge?.label??'';if(edge){document.getElementById('edge-source').value=edge.source;document.getElementById('edge-target').value=edge.target;}else{document.getElementById('edge-source').selectedIndex=0;document.getElementById('edge-target').selectedIndex=Math.min(1,model.nodes.length-1);}document.getElementById('delete-edge').hidden=!edge;showForm(edgeForm);document.getElementById('edge-type').focus();
  }
  function saveNode(event){
    event.preventDefault();const currentId=document.getElementById('node-id').value;const type=document.getElementById('node-type').value.trim(),label=document.getElementById('node-label').value.trim(),variable=document.getElementById('node-variable').value.trim(),description=document.getElementById('node-description').value.trim();
    if(!validIdentifier(type)||!validIdentifier(variable)){setStatus('CLR type and Id variable must be valid C# identifiers.');return;}if(duplicateType(type,model.nodes,currentId)){setStatus(`A node type named ${type} already exists.`);return;}
    if(currentId){const node=nodeById(currentId);Object.assign(node,{type,label,variable,description});hideForm(nodeForm);afterModelChange(`${type} updated.`);return;}
    const id=uniqueId(slug(type,'node'),model.nodes);model.nodes.push({id,type,label,variable,description,x:null,y:null});hideForm(nodeForm);afterModelChange(`${type} added.`);
  }
  function saveEdge(event){
    event.preventDefault();const currentId=document.getElementById('edge-id').value;const type=document.getElementById('edge-type').value.trim(),label=document.getElementById('edge-label').value.trim(),source=document.getElementById('edge-source').value,target=document.getElementById('edge-target').value;
    if(!validIdentifier(type)){setStatus('Edge CLR type must be a valid C# identifier.');return;}if(source===target){setStatus('Choose two different nodes for this documentation edge.');return;}if(duplicateType(type,model.edges,currentId)){setStatus(`An edge type named ${type} already exists.`);return;}
    if(currentId){const edge=edgeById(currentId);Object.assign(edge,{type,label,source,target});hideForm(edgeForm);afterModelChange(`${type} updated.`);return;}
    const id=uniqueId(slug(type,'edge'),model.edges);model.edges.push({id,type,label,source,target});hideForm(edgeForm);afterModelChange(`${type} added.`);
  }
  function deleteNode(){
    const id=document.getElementById('node-id').value,node=nodeById(id);if(!node)return;const connected=model.edges.filter(edge=>edge.source===id||edge.target===id).length;if(!confirm(`Delete ${node.type}${connected?` and ${connected} connected edge${connected===1?'':'s'}`:''}?`))return;
    model.nodes=model.nodes.filter(item=>item.id!==id);model.edges=model.edges.filter(edge=>edge.source!==id&&edge.target!==id);hideForm(nodeForm);hideForm(edgeForm);afterModelChange(`${node.type} deleted${connected?` with ${connected} connected edge${connected===1?'':'s'}`:''}.`);
  }
  function deleteEdge(){const id=document.getElementById('edge-id').value,edge=edgeById(id);if(!edge)return;if(!confirm(`Delete ${edge.type}?`))return;model.edges=model.edges.filter(item=>item.id!==id);hideForm(edgeForm);afterModelChange(`${edge.type} deleted.`);}
  function restoreModel(){if(!confirm('Restore the original demo model and discard your locally saved model?'))return;model=clone(defaultModel);localStorage.removeItem(storageKey);resetPath();hideForm(nodeForm);hideForm(edgeForm);renderDesigner();renderModel();applyPreset('gateway-database');setStatus('Demo model restored.');}
  async function copy(button,value,reset){try{await navigator.clipboard.writeText(value);button.textContent='Copied';setTimeout(()=>button.textContent=reset,1200);}catch{button.textContent='Select manually';}}

  document.querySelectorAll('[data-preset]').forEach(button=>button.addEventListener('click',()=>applyPreset(button.dataset.preset)));
  document.getElementById('reset-path').addEventListener('click',()=>{resetPath();renderAll();});
  document.getElementById('undo-hop').addEventListener('click',()=>{state.hops.pop();renderAll();});
  document.getElementById('copy-path').addEventListener('click',event=>copy(event.currentTarget,pathText(),'Copy path'));
  document.getElementById('copy-query').addEventListener('click',event=>copy(event.currentTarget,buildQuery(),'Copy'));
  document.getElementById('copy-explorer-sql').addEventListener('click',event=>copy(event.currentTarget,buildSql(),'Copy'));
  document.getElementById('open-playground').addEventListener('click',event=>{if(!nodeById(state.root))event.preventDefault();});
  document.getElementById('new-node').addEventListener('click',()=>openNodeForm());
  document.getElementById('new-edge').addEventListener('click',()=>openEdgeForm());
  document.getElementById('restore-model').addEventListener('click',restoreModel);
  document.getElementById('cancel-node').addEventListener('click',()=>hideForm(nodeForm));
  document.getElementById('cancel-edge').addEventListener('click',()=>hideForm(edgeForm));
  document.getElementById('delete-node').addEventListener('click',deleteNode);
  document.getElementById('delete-edge').addEventListener('click',deleteEdge);
  nodeForm.addEventListener('submit',saveNode);edgeForm.addEventListener('submit',saveEdge);

  renderDesigner();renderModel();
  if(localStorage.getItem(storageKey)){setStatus('Local model restored from this browser. Choose a root node to explore it.');renderAll();}else applyPreset('gateway-database');
})();