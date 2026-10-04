(()=>{
  const storageKey='gorm.graphExplorer.model.v2';
  const defaults={
    nodes:[
      {id:'gateway',type:'GatewayNode',label:'Gateway'},
      {id:'service',type:'ServiceNode',label:'Order Service'},
      {id:'database',type:'DatabaseNode',label:'Orders DB'},
      {id:'queue',type:'QueueNode',label:'Order Events'},
      {id:'worker',type:'WorkerNode',label:'Billing Worker'}
    ],
    edges:[
      {id:'routes',type:'RoutesToEdge',source:'gateway',target:'service'},
      {id:'depends',type:'DependsOnEdge',source:'service',target:'database'},
      {id:'publishes',type:'PublishesToEdge',source:'service',target:'queue'},
      {id:'consumes',type:'ConsumesFromEdge',source:'worker',target:'queue'}
    ]
  };
  const $=id=>document.getElementById(id);
  if(!$('graph-canvas')||!$('graph-nodes')||!$('graph-edges'))return;
  const css=document.createElement('link');css.rel='stylesheet';css.href='./graph-explorer-pathfinder.css';document.head.appendChild(css);
  function validModel(value){if(!value||!Array.isArray(value.nodes)||!Array.isArray(value.edges)||!value.nodes.length)return false;const ids=new Set(value.nodes.map(node=>node.id));return value.nodes.every(node=>node.id&&node.type&&node.label)&&value.edges.every(edge=>edge.id&&edge.type&&ids.has(edge.source)&&ids.has(edge.target));}
  function readModel(){try{const raw=localStorage.getItem(storageKey),value=raw?JSON.parse(raw):null;return validModel(value)?value:defaults;}catch{return defaults;}}
  function nodeById(model,id){return model.nodes.find(node=>node.id===id);}
  function connectedEdges(model,id){return model.edges.filter(edge=>edge.source===id||edge.target===id);}
  function relation(edge,from){if(edge.source===from)return{direction:'outgoing',to:edge.target};if(edge.target===from)return{direction:'incoming',to:edge.source};return null;}
  function shortestPath(model,from,to){if(from===to)return[];const queue=[{node:from,hops:[]}],visited=new Set([from]);while(queue.length){const current=queue.shift();for(const edge of connectedEdges(model,current.node)){const next=relation(edge,current.node);if(!next||visited.has(next.to))continue;const hops=[...current.hops,{edge:edge.id,direction:next.direction,from:current.node,to:next.to}];if(next.to===to)return hops;visited.add(next.to);queue.push({node:next.to,hops});}}return null;}
  function install(){if($('pathfinder-shell'))return;const anchor=document.querySelector('.explorer-presets');if(!anchor)return;const section=document.createElement('section');section.id='pathfinder-shell';section.className='pathfinder-shell';section.innerHTML=`<div class="pathfinder-head"><div><span>PATH DISCOVERY</span><h2>Find the shortest typed route.</h2></div><p>Choose two node types. GORM Explorer discovers the shortest route through the current documentation model and replays it through the existing traversal engine.</p></div><div class="pathfinder-controls"><label><span>FROM</span><select id="pathfinder-from" aria-label="Path start node"></select></label><button id="pathfinder-swap" class="tiny-button pathfinder-swap" type="button" title="Swap start and destination">⇄</button><label><span>TO</span><select id="pathfinder-to" aria-label="Path destination node"></select></label><button id="pathfinder-find" class="tiny-button pathfinder-find" type="button">Find path</button></div><div id="pathfinder-result" class="pathfinder-result" hidden aria-live="polite"></div>`;anchor.insertAdjacentElement('afterend',section);$('pathfinder-find').addEventListener('click',findAndApply);$('pathfinder-swap').addEventListener('click',()=>{const from=$('pathfinder-from'),to=$('pathfinder-to'),value=from.value;from.value=to.value;to.value=value;findAndApply();});refreshOptions();}
  function optionText(node){return `${node.label} · ${node.type}`;}
  function refreshOptions(){const model=readModel(),from=$('pathfinder-from'),to=$('pathfinder-to');if(!from||!to)return;const previousFrom=from.value,previousTo=to.value;[from,to].forEach(select=>{select.textContent='';model.nodes.forEach(node=>{const option=document.createElement('option');option.value=node.id;option.textContent=optionText(node);select.appendChild(option);});});if(model.nodes.some(node=>node.id===previousFrom))from.value=previousFrom;if(model.nodes.some(node=>node.id===previousTo))to.value=previousTo;if(!from.value&&model.nodes.length)from.value=model.nodes[0].id;if((!to.value||to.value===from.value)&&model.nodes.length>1)to.value=model.nodes.at(-1).id;}
  function escapeHtml(value){return String(value).replace(/[&<>"']/g,char=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[char]));}
  function routeHtml(model,from,hops){const root=nodeById(model,from);let html=`<span>${escapeHtml(root.type)}</span>`;for(const hop of hops){const edge=model.edges.find(edge=>edge.id===hop.edge),target=nodeById(model,hop.to);html+=hop.direction==='outgoing'?` <span class="outgoing">-[${escapeHtml(edge.type)}]→</span> <span>${escapeHtml(target.type)}</span>`:` <span class="incoming">←[${escapeHtml(edge.type)}]-</span> <span>${escapeHtml(target.type)}</span>`;}return html;}
  function renderResult(model,from,hops){const result=$('pathfinder-result');result.hidden=false;result.classList.toggle('pathfinder-empty',hops===null);if(hops===null){result.innerHTML='<strong>No path</strong><div class="pathfinder-route">These nodes are disconnected in the current model.</div>';return;}const count=hops.length;result.innerHTML=`<strong>${count} hop${count===1?'':'s'}</strong><div class="pathfinder-route">${routeHtml(model,from,hops)}</div>`;}
  function replay(from,hops){const root=document.querySelector(`.graph-node-button[data-node="${CSS.escape(from)}"]`);if(!root)return false;root.click();for(const hop of hops){const edge=document.querySelector(`.graph-edge-group[data-edge="${CSS.escape(hop.edge)}"]`);if(!edge)return false;edge.dispatchEvent(new MouseEvent('click',{bubbles:true}));}document.querySelector('.graph-panel')?.scrollIntoView({behavior:'smooth',block:'start'});return true;}
  function findAndApply(){const model=readModel(),from=$('pathfinder-from').value,to=$('pathfinder-to').value;if(!nodeById(model,from)||!nodeById(model,to)){refreshOptions();return;}const hops=shortestPath(model,from,to);renderResult(model,from,hops);if(hops===null)return;if(!replay(from,hops))renderResult(model,from,null);}
  install();const observer=new MutationObserver(()=>refreshOptions());observer.observe($('graph-nodes'),{childList:true,subtree:false});
})();