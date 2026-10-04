(()=>{
  const storageKey='gorm.graphExplorer.model.v2';
  const svgNs='http://www.w3.org/2000/svg';
  const clone=value=>JSON.parse(JSON.stringify(value));
  const defaults={nodes:[
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
  const presets={
    'gateway-database':['gateway','routes','depends'],
    'service-queue':['service','publishes'],
    'database-dependents':['database','depends'],
    'queue-consumers':['queue','consumes']
  };
  const state={root:null,hops:[]};
  const $=id=>document.getElementById(id);
  const identifier=/^[A-Za-z_][A-Za-z0-9_]*$/;
  let model=loadModel();

  if(!$('graph-canvas')||!$('graph-edges')||!$('graph-nodes'))return;
  const extraCss=document.createElement('link');extraCss.rel='stylesheet';extraCss.href='./graph-explorer-designer.css';document.head.appendChild(extraCss);

  function isValid(value){
    if(!value||!Array.isArray(value.nodes)||!Array.isArray(value.edges)||!value.nodes.length)return false;
    const ids=new Set(value.nodes.map(x=>x.id));
    return value.nodes.every(x=>x.id&&x.type&&x.label&&x.variable)&&value.edges.every(x=>x.id&&x.type&&ids.has(x.source)&&ids.has(x.target));
  }
  function loadModel(){try{const raw=localStorage.getItem(storageKey);const value=raw?JSON.parse(raw):null;return isValid(value)?value:clone(defaults);}catch{return clone(defaults);}}
  function saveModel(){try{localStorage.setItem(storageKey,JSON.stringify(model));}catch{}}
  function node(id){return model.nodes.find(x=>x.id===id);}
  function edge(id){return model.edges.find(x=>x.id===id);}
  function current(){return state.hops.at(-1)?.to??state.root;}
  function usedEdges(){return new Set(state.hops.map(x=>x.edge));}
  function relation(item,from=current()){
    if(item.source===from)return{direction:'outgoing',to:item.target};
    if(item.target===from)return{direction:'incoming',to:item.source};
    return null;
  }
  function available(){const id=current(),used=usedEdges();return id?model.edges.filter(x=>!used.has(x.id)&&(x.source===id||x.target===id)):[];}
  function status(text){$('designer-status').textContent=text;}
  function resetPath(){state.root=null;state.hops=[];}
  function changed(text){saveModel();resetPath();renderDesigner();renderGraph();renderPath();renderOutput();status(`${text} Traversal path reset.`);}

  function layout(){
    if(model.nodes.length<=5&&model.nodes.every(x=>Number.isFinite(x.x)&&Number.isFinite(x.y)))return;
    const columns=model.nodes.length<=4?2:model.nodes.length<=9?3:4,rows=Math.ceil(model.nodes.length/columns);
    model.nodes.forEach((item,index)=>{const col=index%columns,row=Math.floor(index/columns);item.x=120+760*(col/Math.max(1,columns-1));item.y=95+370*(row/Math.max(1,rows-1));});
  }
  function svg(name,attrs={}){const element=document.createElementNS(svgNs,name);Object.entries(attrs).forEach(([key,value])=>element.setAttribute(key,String(value)));return element;}
  function marker(defs,id,color){const item=svg('marker',{id,viewBox:'0 0 10 10',refX:'8.5',refY:'5',markerWidth:'7',markerHeight:'7',orient:'auto-start-reverse'});item.appendChild(svg('path',{d:'M 0 0 L 10 5 L 0 10 z',fill:color}));defs.appendChild(item);}
  function line(a,b,padding=82){const dx=b.x-a.x,dy=b.y-a.y,length=Math.hypot(dx,dy)||1,ux=dx/length,uy=dy/length;return{x1:a.x+ux*padding,y1:a.y+uy*padding,x2:b.x-ux*padding,y2:b.y-uy*padding};}
  function renderGraph(){
    layout();const host=$('graph-nodes'),edges=$('graph-edges');host.textContent='';edges.textContent='';
    const defs=svg('defs');marker(defs,'arrow-default','#4a4552');marker(defs,'arrow-selectable','#b797ff');marker(defs,'arrow-selected','#9fe3d5');edges.appendChild(defs);
    model.edges.forEach(item=>{
      const source=node(item.source),target=node(item.target);if(!source||!target)return;const points=line(source,target),group=svg('g',{class:'graph-edge-group','data-edge':item.id,tabindex:'0',role:'button'}),midX=(points.x1+points.x2)/2,midY=(points.y1+points.y2)/2,width=Math.max(116,item.type.length*7.3+24);
      group.append(svg('line',{class:'graph-edge-hit',...points}),svg('line',{class:'graph-edge-line',...points,'marker-end':'url(#arrow-default)'}),svg('rect',{class:'graph-edge-label-bg',x:midX-width/2,y:midY-15,width,height:30,rx:10}));
      const label=svg('text',{class:'graph-edge-label',x:midX,y:midY});label.textContent=item.type;group.appendChild(label);group.addEventListener('click',()=>follow(item.id));group.addEventListener('keydown',event=>{if(event.key==='Enter'||event.key===' '){event.preventDefault();follow(item.id);}});edges.appendChild(group);
    });
    model.nodes.forEach(item=>{const button=document.createElement('button');button.type='button';button.className='graph-node-button';button.dataset.node=item.id;button.style.left=`${item.x/10}%`;button.style.top=`${item.y/5.6}%`;button.innerHTML=`<small></small><strong></strong>`;button.querySelector('small').textContent=item.type;button.querySelector('strong').textContent=item.label;button.addEventListener('click',()=>start(item.id));host.appendChild(button);});
    updateGraphState();
  }
  function updateGraphState(){
    const selected=new Set(state.hops.map(x=>x.edge)),next=new Set(available().map(x=>x.id)),now=current();
    document.querySelectorAll('.graph-node-button').forEach(x=>{x.classList.toggle('root',x.dataset.node===state.root);x.classList.toggle('current',x.dataset.node===now);});
    $('graph-edges').querySelectorAll('.graph-edge-group').forEach(group=>{const id=group.dataset.edge,isSelected=selected.has(id),isNext=next.has(id);group.classList.toggle('selected',isSelected);group.classList.toggle('selectable',!isSelected&&isNext);group.classList.toggle('dim',Boolean(now)&&!isSelected&&!isNext);group.querySelector('.graph-edge-line').setAttribute('marker-end',isSelected?'url(#arrow-selected)':isNext?'url(#arrow-selectable)':'url(#arrow-default)');});
  }
  function start(id){if(!node(id))return;state.root=id;state.hops=[];renderTraversal();}
  function follow(id){const item=edge(id),next=item?relation(item):null;if(!next||usedEdges().has(id))return;state.hops.push({edge:id,direction:next.direction,to:next.to});renderTraversal();}
  function renderTraversal(){updateGraphState();renderPath();renderOutput();}
  function applyPreset(key){
    const [root,...edges]=presets[key]??[];if(!node(root)||edges.some(id=>!edge(id))){status('This preset is not available in the custom model. Restore the demo model to use it.');return;}state.root=root;state.hops=[];edges.forEach(id=>{const item=edge(id),next=relation(item);if(next)state.hops.push({edge:id,direction:next.direction,to:next.to});});renderTraversal();
  }

  function chip(host,kicker,value,className){const item=document.createElement('div');item.className=`path-chip ${className}`;const small=document.createElement('small'),strong=document.createElement('strong');small.textContent=kicker;strong.textContent=value;item.append(small,strong);host.appendChild(item);}
  function renderPath(){
    const flow=$('path-flow'),details=$('current-node'),root=node(state.root);flow.textContent='';details.textContent='';
    if(!root){$('path-title').textContent='Choose a root node';flow.innerHTML='<p>Select any node in the model to begin.</p>';return;}
    const now=node(current());$('path-title').textContent=state.hops.length?`${state.hops.length} hop${state.hops.length===1?'':'s'} · ${now.type}`:`Root · ${root.type}`;chip(flow,'ROOT',root.type,'node');
    state.hops.forEach(hop=>{const item=edge(hop.edge),target=node(hop.to),arrow=document.createElement('span');arrow.className='path-arrow';arrow.textContent=hop.direction==='outgoing'?'→':'←';flow.appendChild(arrow);chip(flow,hop.direction.toUpperCase(),item.type,`edge ${hop.direction}`);const next=document.createElement('span');next.className='path-arrow';next.textContent='→';flow.appendChild(next);chip(flow,'NODE',target.type,'node');});
    details.innerHTML='<span>CURRENT NODE</span><h3></h3><p></p>';details.querySelector('h3').textContent=now.label;details.querySelector('p').textContent=now.description||`${now.type} in the current model.`;
    const list=document.createElement('div');list.className='available-list';available().forEach(item=>{const next=relation(item),target=node(next.to),button=document.createElement('button');button.type='button';button.innerHTML='<span></span><small></small>';button.querySelector('span').textContent=`${next.direction==='outgoing'?'Outgoing':'Incoming'} ${item.type}`;button.querySelector('small').textContent=target.type;button.addEventListener('click',()=>follow(item.id));list.appendChild(button);});if(list.children.length)details.appendChild(list);
  }
  function query(){const root=node(state.root);if(!root)return'// Choose a root node to generate a traversal.';const lines=[`var result = await context.Set<${root.type}>()`,`    .Where(x => x.Id == ${root.variable})`];state.hops.forEach(hop=>{const item=edge(hop.edge),target=node(hop.to);lines.push(`    .${hop.direction==='outgoing'?'Outgoing':'Incoming'}<${item.type}, ${target.type}>()`);});return [...lines,'    .ToListAsync();'].join('\n');}
  function sql(){const root=node(state.root);if(!root)return'-- Choose a root node to generate the SQL shape.';const tables=[`[${root.type}] AS node0`],matches=[];state.hops.forEach((hop,index)=>{const item=edge(hop.edge),target=node(hop.to);tables.push(`[${item.type}] AS edge${index}`,`[${target.type}] AS node${index+1}`);matches.push(hop.direction==='outgoing'?`MATCH(node${index}-(edge${index})->node${index+1})`:`MATCH(node${index+1}-(edge${index})->node${index})`);});let value=`-- Documentation query shape\nSELECT node${state.hops.length}.*\nFROM ${tables.join(',\n     ')}\nWHERE `;if(matches.length)value+=`${matches.join('\n  AND ')}\n  AND `;return`${value}node0.[Id] = @${root.variable};`;}
  function encode(value){let binary='';new TextEncoder().encode(value).forEach(byte=>binary+=String.fromCharCode(byte));return btoa(binary).replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');}
  function renderOutput(){const hasRoot=Boolean(node(state.root)),text=query();$('gorm-query').textContent=text;$('graph-sql').textContent=sql();['copy-query','copy-explorer-sql','copy-path'].forEach(id=>$(id).disabled=!hasRoot);$('undo-hop').disabled=!state.hops.length;const link=$('open-playground');link.href=hasRoot?`./playground.html?q=${encodeURIComponent(encode(text))}`:'./playground.html';link.classList.toggle('disabled',!hasRoot);link.setAttribute('aria-disabled',String(!hasRoot));}
  function pathText(){const root=node(state.root);if(!root)return'';let value=root.type;state.hops.forEach(hop=>{const item=edge(hop.edge),target=node(hop.to);value+=hop.direction==='outgoing'?` -[${item.type}]-> ${target.type}`:` <-[${item.type}]- ${target.type}`;});return value;}

  function slug(value,fallback){return value.trim().replace(/([a-z0-9])([A-Z])/g,'$1-$2').replace(/[^A-Za-z0-9]+/g,'-').replace(/^-|-$/g,'').toLowerCase()||fallback;}
  function unique(base,items){let value=base,index=2;while(items.some(x=>x.id===value))value=`${base}-${index++}`;return value;}
  function duplicateType(value,items,currentId){return items.some(x=>x.id!==currentId&&x.type.toLowerCase()===value.toLowerCase());}
  function nodeOptions(){['edge-source','edge-target'].forEach(id=>{const select=$(id),selected=select.value;select.textContent='';model.nodes.forEach(item=>{const option=document.createElement('option');option.value=item.id;option.textContent=`${item.label} · ${item.type}`;select.appendChild(option);});if(node(selected))select.value=selected;});}
  function renderDesigner(){
    $('node-count').textContent=`${model.nodes.length} node${model.nodes.length===1?'':'s'}`;$('edge-count').textContent=`${model.edges.length} edge${model.edges.length===1?'':'s'}`;
    const nodes=$('node-list');nodes.textContent='';model.nodes.forEach(item=>nodes.appendChild(modelButton(item.label,`${item.type} · ${item.variable}`,()=>openNode(item.id))));
    const edges=$('edge-list');edges.textContent='';model.edges.forEach(item=>edges.appendChild(modelButton(item.type,`${node(item.source)?.label} → ${node(item.target)?.label}`,()=>openEdge(item.id))));
    nodeOptions();$('new-edge').disabled=model.nodes.length<2;
  }
  function modelButton(title,meta,action){const button=document.createElement('button');button.type='button';button.className='model-item';button.innerHTML='<span><strong></strong><small></small></span><b>Edit</b>';button.querySelector('strong').textContent=title;button.querySelector('small').textContent=meta;button.addEventListener('click',action);return button;}
  function openNode(id=''){const item=node(id);$('node-form-title').textContent=item?'Edit node':'Add node';$('node-id').value=item?.id??'';$('node-type').value=item?.type??'';$('node-label').value=item?.label??'';$('node-variable').value=item?.variable??'';$('node-description').value=item?.description??'';$('delete-node').hidden=!item;$('node-form').hidden=false;$('node-type').focus();}
  function openEdge(id=''){if(model.nodes.length<2){status('Add at least two nodes before creating an edge.');return;}const item=edge(id);nodeOptions();$('edge-form-title').textContent=item?'Edit edge':'Add edge';$('edge-id').value=item?.id??'';$('edge-type').value=item?.type??'';$('edge-label').value=item?.label??'';if(item){$('edge-source').value=item.source;$('edge-target').value=item.target;}else{$('edge-source').selectedIndex=0;$('edge-target').selectedIndex=1;}$('delete-edge').hidden=!item;$('edge-form').hidden=false;$('edge-type').focus();}
  function saveNode(event){event.preventDefault();const id=$('node-id').value,type=$('node-type').value.trim(),label=$('node-label').value.trim(),variable=$('node-variable').value.trim(),description=$('node-description').value.trim();if(!identifier.test(type)||!identifier.test(variable)){status('CLR type and Id variable must be valid C# identifiers.');return;}if(duplicateType(type,model.nodes,id)){status(`A node type named ${type} already exists.`);return;}if(id)Object.assign(node(id),{type,label,variable,description});else model.nodes.push({id:unique(slug(type,'node'),model.nodes),type,label,variable,description,x:null,y:null});$('node-form').hidden=true;changed(`${type} ${id?'updated':'added'}.`);}
  function saveEdge(event){event.preventDefault();const id=$('edge-id').value,type=$('edge-type').value.trim(),label=$('edge-label').value.trim(),source=$('edge-source').value,target=$('edge-target').value;if(!identifier.test(type)){status('Edge CLR type must be a valid C# identifier.');return;}if(source===target){status('Choose two different nodes for this documentation edge.');return;}if(duplicateType(type,model.edges,id)){status(`An edge type named ${type} already exists.`);return;}if(id)Object.assign(edge(id),{type,label,source,target});else model.edges.push({id:unique(slug(type,'edge'),model.edges),type,label,source,target});$('edge-form').hidden=true;changed(`${type} ${id?'updated':'added'}.`);}
  function deleteNode(){const item=node($('node-id').value);if(!item)return;const connected=model.edges.filter(x=>x.source===item.id||x.target===item.id);if(!confirm(`Delete ${item.type}${connected.length?` and ${connected.length} connected edge${connected.length===1?'':'s'}`:''}?`))return;model.nodes=model.nodes.filter(x=>x.id!==item.id);model.edges=model.edges.filter(x=>x.source!==item.id&&x.target!==item.id);$('node-form').hidden=true;$('edge-form').hidden=true;changed(`${item.type} deleted.`);}
  function deleteEdge(){const item=edge($('edge-id').value);if(!item||!confirm(`Delete ${item.type}?`))return;model.edges=model.edges.filter(x=>x.id!==item.id);$('edge-form').hidden=true;changed(`${item.type} deleted.`);}
  function restore(){if(!confirm('Restore the original demo model and discard your locally saved model?'))return;model=clone(defaults);try{localStorage.removeItem(storageKey);}catch{}resetPath();$('node-form').hidden=true;$('edge-form').hidden=true;renderDesigner();renderGraph();applyPreset('gateway-database');status('Demo model restored.');}
  async function copy(button,value,label){try{await navigator.clipboard.writeText(value);button.textContent='Copied';setTimeout(()=>button.textContent=label,1000);}catch{button.textContent='Select manually';}}

  document.querySelectorAll('[data-preset]').forEach(button=>button.addEventListener('click',()=>applyPreset(button.dataset.preset)));
  $('reset-path').addEventListener('click',()=>{resetPath();renderTraversal();});$('undo-hop').addEventListener('click',()=>{state.hops.pop();renderTraversal();});$('copy-path').addEventListener('click',event=>copy(event.currentTarget,pathText(),'Copy path'));$('copy-query').addEventListener('click',event=>copy(event.currentTarget,query(),'Copy'));$('copy-explorer-sql').addEventListener('click',event=>copy(event.currentTarget,sql(),'Copy'));
  $('new-node').addEventListener('click',()=>openNode());$('new-edge').addEventListener('click',()=>openEdge());$('restore-model').addEventListener('click',restore);$('cancel-node').addEventListener('click',()=>$('node-form').hidden=true);$('cancel-edge').addEventListener('click',()=>$('edge-form').hidden=true);$('delete-node').addEventListener('click',deleteNode);$('delete-edge').addEventListener('click',deleteEdge);$('node-form').addEventListener('submit',saveNode);$('edge-form').addEventListener('submit',saveEdge);

  renderDesigner();renderGraph();
  try{if(localStorage.getItem(storageKey)){renderTraversal();status('Local model restored from this browser. Choose a root node to explore it.');}else applyPreset('gateway-database');}catch{applyPreset('gateway-database');}
})();