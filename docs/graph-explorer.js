(()=>{
  const svgNs='http://www.w3.org/2000/svg';
  const model={
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
  const nodesById=new Map(model.nodes.map(node=>[node.id,node]));
  const edgesById=new Map(model.edges.map(edge=>[edge.id,edge]));
  const canvas=document.getElementById('graph-canvas');
  const svg=document.getElementById('graph-edges');
  const nodesHost=document.getElementById('graph-nodes');
  if(!canvas||!svg||!nodesHost)return;

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
  function shortenedLine(source,target,padding=82){
    const dx=target.x-source.x,dy=target.y-source.y,length=Math.hypot(dx,dy)||1,ux=dx/length,uy=dy/length;
    return{x1:source.x+ux*padding,y1:source.y+uy*padding,x2:target.x-ux*padding,y2:target.y-uy*padding};
  }
  function renderModel(){
    svg.textContent='';addMarkers();nodesHost.textContent='';
    model.edges.forEach(edge=>{
      const source=nodesById.get(edge.source),target=nodesById.get(edge.target);const line=shortenedLine(source,target);
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

  function setRoot(nodeId){state.root=nodeId;state.hops=[];renderAll();}
  function extendByEdge(edgeId){
    const edge=edgesById.get(edgeId);const relation=edge?relationFor(edge):null;if(!relation||usedEdgeIds().has(edgeId))return;
    state.hops.push({edge:edgeId,direction:relation.direction,from:currentNodeId(),to:relation.to});renderAll();
  }
  function applyPreset(key){
    const preset=presets[key];if(!preset)return;state.root=preset.root;state.hops=[];
    preset.edges.forEach(edgeId=>{const edge=edgesById.get(edgeId);const relation=edge?relationFor(edge):null;if(relation)state.hops.push({edge:edgeId,direction:relation.direction,from:currentNodeId(),to:relation.to});});
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
    if(!state.root)return'';let text=nodesById.get(state.root).type;
    state.hops.forEach(hop=>{const edge=edgesById.get(hop.edge),target=nodesById.get(hop.to);text+=hop.direction==='outgoing'?` -[${edge.type}]-> ${target.type}`:` <-[${edge.type}]- ${target.type}`;});return text;
  }
  function renderPath(){
    const flow=document.getElementById('path-flow'),title=document.getElementById('path-title'),details=document.getElementById('current-node');flow.textContent='';details.textContent='';
    if(!state.root){const empty=document.createElement('p');empty.textContent='Select any node in the model to begin.';flow.appendChild(empty);title.textContent='Choose a root node';return;}
    const root=nodesById.get(state.root);title.textContent=state.hops.length?`${state.hops.length} hop${state.hops.length===1?'':'s'} · ${nodesById.get(currentNodeId()).type}`:`Root · ${root.type}`;
    appendChip(flow,'ROOT',root.type,'node');
    state.hops.forEach(hop=>{const edge=edgesById.get(hop.edge),target=nodesById.get(hop.to);const arrow=document.createElement('span');arrow.className='path-arrow';arrow.textContent=hop.direction==='outgoing'?'→':'←';flow.appendChild(arrow);appendChip(flow,hop.direction.toUpperCase(),edge.type,`edge ${hop.direction}`);const next=document.createElement('span');next.className='path-arrow';next.textContent='→';flow.appendChild(next);appendChip(flow,'NODE',target.type,'node');});
    const current=nodesById.get(currentNodeId());const kicker=document.createElement('span');kicker.textContent='CURRENT NODE';const heading=document.createElement('h3');heading.textContent=current.label;const paragraph=document.createElement('p');paragraph.textContent=current.description;details.append(kicker,heading,paragraph);
    const relations=availableEdges();if(relations.length){const list=document.createElement('div');list.className='available-list';relations.forEach(edge=>{const relation=relationFor(edge),target=nodesById.get(relation.to);const button=document.createElement('button');button.type='button';const text=document.createElement('span');text.textContent=`${relation.direction==='outgoing'?'Outgoing':'Incoming'} ${edge.type}`;const small=document.createElement('small');small.textContent=target.type;button.append(text,small);button.addEventListener('click',()=>extendByEdge(edge.id));list.appendChild(button);});details.appendChild(list);}else{const done=document.createElement('p');done.textContent='No unused relationships continue from this node.';details.appendChild(done);}
  }
  function appendChip(host,label,value,className){const chip=document.createElement('div');chip.className=`path-chip ${className}`;const small=document.createElement('small');small.textContent=label;const strong=document.createElement('strong');strong.textContent=value;chip.append(small,strong);host.appendChild(chip);}

  function buildQuery(){
    if(!state.root)return'// Choose a root node to generate a traversal.';const root=nodesById.get(state.root);const lines=[`var result = await context.Set<${root.type}>()`,`    .Where(x => x.Id == ${root.variable})`];
    state.hops.forEach(hop=>{const edge=edgesById.get(hop.edge),target=nodesById.get(hop.to),method=hop.direction==='outgoing'?'Outgoing':'Incoming';lines.push(`    .${method}<${edge.type}, ${target.type}>()`);});lines.push('    .ToListAsync();');return lines.join('\n');
  }
  function buildSql(){
    if(!state.root)return'-- Choose a root node to generate the SQL shape.';const root=nodesById.get(state.root);const tables=[`[${root.type}] AS node0`],matches=[];
    state.hops.forEach((hop,index)=>{const edge=edgesById.get(hop.edge),target=nodesById.get(hop.to);tables.push(`[${edge.type}] AS edge${index}`,`[${target.type}] AS node${index+1}`);matches.push(hop.direction==='outgoing'?`MATCH(node${index}-(edge${index})->node${index+1})`:`MATCH(node${index+1}-(edge${index})->node${index})`);});
    const resultAlias=`node${state.hops.length}`;let sql=`-- Documentation query shape\nSELECT ${resultAlias}.*\nFROM ${tables.join(',\n     ')}\nWHERE `;if(matches.length)sql+=`${matches.join('\n  AND ')}\n  AND `;sql+=`node0.[Id] = @${root.variable};`;return sql;
  }
  function encodeQuery(value){const bytes=new TextEncoder().encode(value);let binary='';bytes.forEach(byte=>binary+=String.fromCharCode(byte));return btoa(binary).replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');}
  function playgroundUrl(query){const url=new URL('./playground.html',location.href);url.searchParams.set('q',encodeQuery(query));return url.href;}
  function renderOutput(){
    const query=buildQuery(),sql=buildSql(),hasRoot=Boolean(state.root);document.getElementById('gorm-query').textContent=query;document.getElementById('graph-sql').textContent=sql;
    ['copy-query','copy-explorer-sql','copy-path'].forEach(id=>{document.getElementById(id).disabled=!hasRoot;});document.getElementById('undo-hop').disabled=!state.hops.length;
    const link=document.getElementById('open-playground');link.href=hasRoot?playgroundUrl(query):'./playground.html';link.classList.toggle('disabled',!hasRoot);link.setAttribute('aria-disabled',String(!hasRoot));
  }
  function renderAll(){updateGraphState();renderPath();renderOutput();}
  async function copy(button,value,reset){try{await navigator.clipboard.writeText(value);button.textContent='Copied';setTimeout(()=>button.textContent=reset,1200);}catch{button.textContent='Select manually';}}

  document.querySelectorAll('[data-preset]').forEach(button=>button.addEventListener('click',()=>applyPreset(button.dataset.preset)));
  document.getElementById('reset-path').addEventListener('click',()=>{state.root=null;state.hops=[];renderAll();});
  document.getElementById('undo-hop').addEventListener('click',()=>{state.hops.pop();renderAll();});
  document.getElementById('copy-path').addEventListener('click',event=>copy(event.currentTarget,pathText(),'Copy path'));
  document.getElementById('copy-query').addEventListener('click',event=>copy(event.currentTarget,buildQuery(),'Copy'));
  document.getElementById('copy-explorer-sql').addEventListener('click',event=>copy(event.currentTarget,buildSql(),'Copy'));
  document.getElementById('open-playground').addEventListener('click',event=>{if(!state.root)event.preventDefault();});

  renderModel();applyPreset('gateway-database');
})();