(()=>{
  const maxHops=20;
  const $=id=>document.getElementById(id);

  function currentTopology(){
    const nodes=[...document.querySelectorAll('.graph-node-button')].map(button=>({id:button.dataset.node,type:button.querySelector('small')?.textContent?.trim()??button.dataset.node,label:button.querySelector('strong')?.textContent?.trim()??button.dataset.node}));
    const byType=new Map(nodes.map(node=>[node.type,node.id]));
    const edges=[];
    for(const group of document.querySelectorAll('.graph-edge-group')){
      const match=(group.getAttribute('aria-label')??'').match(/^([^:]+):\s+([^\s]+)\s+to\s+([^\s]+)$/);
      if(!match)continue;
      const [,type,sourceType,targetType]=match,source=byType.get(sourceType),target=byType.get(targetType);
      if(source&&target)edges.push({id:group.dataset.edge,type,source,target});
    }
    return{nodes,edges};
  }

  function shortestRoute(topology,source,target){
    if(source===target)return[];
    const queue=[{node:source,route:[]}],visited=new Set([source]);
    while(queue.length){
      const current=queue.shift();
      if(current.route.length>=maxHops)continue;
      for(const edge of topology.edges){
        let next=null;
        if(edge.source===current.node)next=edge.target;
        else if(edge.target===current.node)next=edge.source;
        if(!next||visited.has(next))continue;
        const route=[...current.route,edge.id];
        if(next===target)return route;
        visited.add(next);queue.push({node:next,route});
      }
    }
    return null;
  }

  function routeDescription(topology,source,edgeIds){
    const nodes=new Map(topology.nodes.map(node=>[node.id,node])),edges=new Map(topology.edges.map(edge=>[edge.id,edge]));
    let current=source,text=nodes.get(source)?.type??source;
    for(const edgeId of edgeIds){
      const edge=edges.get(edgeId);if(!edge)continue;
      const outgoing=edge.source===current,next=outgoing?edge.target:edge.source;
      text+=outgoing?` → ${edge.type} → ${nodes.get(next)?.type??next}`:` ← ${edge.type} ← ${nodes.get(next)?.type??next}`;current=next;
    }
    return text;
  }

  function populate(){
    const from=$('route-from'),to=$('route-to');if(!from||!to)return;
    const topology=currentTopology(),oldFrom=from.value,oldTo=to.value;
    for(const select of[from,to]){select.textContent='';topology.nodes.forEach(node=>{const option=document.createElement('option');option.value=node.id;option.textContent=`${node.label} · ${node.type}`;select.appendChild(option);});}
    if(topology.nodes.some(node=>node.id===oldFrom))from.value=oldFrom;else if(topology.nodes.length)from.value=topology.nodes[0].id;
    if(topology.nodes.some(node=>node.id===oldTo))to.value=oldTo;else if(topology.nodes.length>1)to.value=topology.nodes.at(-1).id;
    $('find-route').disabled=topology.nodes.length<1;
  }

  function applyRoute(source,edgeIds){
    const root=document.querySelector(`.graph-node-button[data-node="${CSS.escape(source)}"]`);if(!root)return false;
    root.click();
    if(!root.classList.contains('root'))root.click();
    for(const edgeId of edgeIds){
      const group=document.querySelector(`.graph-edge-group[data-edge="${CSS.escape(edgeId)}"]`);if(!group)return false;group.dispatchEvent(new MouseEvent('click',{bubbles:true}));
    }
    return true;
  }

  function find(){
    const source=$('route-from')?.value,target=$('route-to')?.value,result=$('route-finder-result');if(!source||!target||!result)return;
    const topology=currentTopology(),route=shortestRoute(topology,source,target);result.classList.remove('route-found','route-missing');
    if(route===null){result.classList.add('route-missing');result.innerHTML='<span><strong>No route found.</strong> These node types are disconnected in the current model.</span><span>Model topology only</span>';return;}
    if(!applyRoute(source,route)){result.classList.add('route-missing');result.innerHTML='<span><strong>Route changed while applying it.</strong> Try again after the model finishes updating.</span><span>Model topology only</span>';return;}
    const description=routeDescription(topology,source,route);result.classList.add('route-found');result.textContent='';
    const message=document.createElement('span'),strong=document.createElement('strong');strong.textContent=route.length===0?'Same node type. ':`${route.length} hop${route.length===1?'':'s'} found. `;message.append(strong,document.createTextNode(description));
    const note=document.createElement('span');note.textContent=`Shortest typed route · max ${maxHops} hops`;result.append(message,note);
  }

  function install(){
    const presets=document.querySelector('.explorer-presets');if(!presets||$('route-finder'))return;
    const style=document.createElement('link');style.rel='stylesheet';style.href='./graph-explorer-route-finder.css';document.head.appendChild(style);
    const panel=document.createElement('section');panel.id='route-finder';panel.className='route-finder';
    panel.innerHTML='<div class="route-finder-head"><div><span>TRAVERSAL ROUTE FINDER</span><strong>Find the shortest typed route between two node types.</strong><p>This searches the editable model topology and composes the existing Outgoing&lt;&gt; / Incoming&lt;&gt; traversal chain. It does not query database records or introduce a new GORM shortest-path operator.</p></div><div class="route-finder-badge">MODEL ROUTE · ≤20 HOPS</div></div><div class="route-finder-controls"><label><span>FROM</span><select id="route-from" aria-label="Route source node"></select></label><div class="route-finder-arrow">→</div><label><span>TO</span><select id="route-to" aria-label="Route target node"></select></label><button id="find-route" type="button" class="tiny-button">Find typed route</button></div><div id="route-finder-result" class="route-finder-result"><span><strong>Choose two node types.</strong> The resulting traversal will appear in the existing path and query panels.</span><span>Model topology only</span></div>';
    presets.insertAdjacentElement('afterend',panel);$('find-route').addEventListener('click',find);populate();
    const observer=new MutationObserver(()=>queueMicrotask(populate));
    observer.observe($('graph-nodes'),{childList:true});
  }

  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',install,{once:true});else install();
})();