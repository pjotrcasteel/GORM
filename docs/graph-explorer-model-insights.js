(()=>{
  const storageKey='gorm.graphExplorer.model.v2';
  const defaults={nodes:[
    {id:'gateway',type:'GatewayNode',label:'Gateway'},
    {id:'service',type:'ServiceNode',label:'Order Service'},
    {id:'database',type:'DatabaseNode',label:'Orders DB'},
    {id:'queue',type:'QueueNode',label:'Order Events'},
    {id:'worker',type:'WorkerNode',label:'Billing Worker'}
  ],edges:[
    {id:'routes',type:'RoutesToEdge',source:'gateway',target:'service'},
    {id:'depends',type:'DependsOnEdge',source:'service',target:'database'},
    {id:'publishes',type:'PublishesToEdge',source:'service',target:'queue'},
    {id:'consumes',type:'ConsumesFromEdge',source:'worker',target:'queue'}
  ]};
  const $=id=>document.getElementById(id);
  if(!$('graph-canvas')||!$('graph-nodes'))return;
  const css=document.createElement('link');css.rel='stylesheet';css.href='./graph-explorer-model-insights.css';document.head.appendChild(css);

  function validModel(value){if(!value||!Array.isArray(value.nodes)||!Array.isArray(value.edges)||!value.nodes.length)return false;const ids=new Set(value.nodes.map(node=>node.id));return value.nodes.every(node=>node.id&&node.type&&node.label)&&value.edges.every(edge=>edge.id&&edge.type&&ids.has(edge.source)&&ids.has(edge.target));}
  function readModel(){try{const raw=localStorage.getItem(storageKey),value=raw?JSON.parse(raw):null;return validModel(value)?value:defaults;}catch{return defaults;}}
  function analyze(graph){
    const degrees=new Map(graph.nodes.map(node=>[node.id,{node,incoming:0,outgoing:0,total:0}]));
    const adjacency=new Map(graph.nodes.map(node=>[node.id,new Set()]));
    graph.edges.forEach(edge=>{const source=degrees.get(edge.source),target=degrees.get(edge.target);if(source){source.outgoing++;source.total++;}if(target){target.incoming++;target.total++;}adjacency.get(edge.source)?.add(edge.target);adjacency.get(edge.target)?.add(edge.source);});
    const components=[],visited=new Set();
    graph.nodes.forEach(node=>{if(visited.has(node.id))return;const component=[],queue=[node.id];visited.add(node.id);while(queue.length){const id=queue.shift();component.push(id);adjacency.get(id)?.forEach(next=>{if(!visited.has(next)){visited.add(next);queue.push(next);}});}components.push(component);});
    const ranked=[...degrees.values()].sort((a,b)=>b.total-a.total||a.node.type.localeCompare(b.node.type));
    const isolated=ranked.filter(item=>item.total===0),sources=ranked.filter(item=>item.incoming===0&&item.outgoing>0),sinks=ranked.filter(item=>item.outgoing===0&&item.incoming>0),maxDegree=ranked[0]?.total??0;
    return{degrees,ranked,components,isolated,sources,sinks,maxDegree};
  }
  function install(){if($('model-insights-shell'))return;const anchor=$('pathfinder-shell')??$('find-focus-shell')??document.querySelector('.explorer-presets');if(!anchor)return;const section=document.createElement('section');section.id='model-insights-shell';section.className='model-insights-shell';section.innerHTML=`<div class="model-insights-head"><div><span>MODEL INSIGHTS</span><h2>Understand the shape before you query it.</h2></div><p>Deterministic analysis of the current documentation model: connectivity, degree and directional boundaries. No database data is queried or changed.</p></div><div id="model-insights-stats" class="model-insights-stats"></div><div class="model-insights-grid"><article class="model-insight-panel"><span>CONNECTIVITY</span><h3 id="model-connectivity-title">Analyzing model…</h3><div id="model-connectivity"></div></article><article class="model-insight-panel"><span>MOST CONNECTED</span><h3>Degree by node type</h3><div id="model-degree-list" class="model-insight-list"></div></article></div>`;anchor.insertAdjacentElement('afterend',section);render();}
  function stat(label,value,detail){const item=document.createElement('div');item.className='model-insight-stat';item.innerHTML='<small></small><strong></strong><em></em>';item.querySelector('small').textContent=label;item.querySelector('strong').textContent=String(value);item.querySelector('em').textContent=detail;return item;}
  function render(){if(!$('model-insights-shell'))return;const graph=readModel(),analysis=analyze(graph),stats=$('model-insights-stats');stats.textContent='';stats.append(stat('NODE TYPES',graph.nodes.length,'model entities'),stat('EDGE TYPES',graph.edges.length,'typed relations'),stat('COMPONENTS',analysis.components.length,'direction ignored'),stat('MAX DEGREE',analysis.maxDegree,'incoming + outgoing'));
    renderConnectivity(graph,analysis);renderDegrees(analysis);
  }
  function renderConnectivity(graph,analysis){const host=$('model-connectivity');host.textContent='';const connected=analysis.components.length===1,$title=$('model-connectivity-title');$title.textContent=connected?'One connected model':`${analysis.components.length} disconnected components`;const health=document.createElement('div');health.className=`model-health${connected&&!analysis.isolated.length?'':' warn'}`;health.innerHTML='<i></i><span></span>';health.querySelector('span').textContent=connected&&!analysis.isolated.length?'Connected':'Review connectivity';host.appendChild(health);
    const note=document.createElement('p');note.className='model-insight-note';note.style.marginTop='10px';note.textContent=analysis.isolated.length?`${analysis.isolated.length} isolated node type${analysis.isolated.length===1?'':'s'}: ${analysis.isolated.map(item=>item.node.type).join(', ')}.`:'Every node type participates in at least one relationship.';host.appendChild(note);
    const components=document.createElement('div');components.className='model-component-list';components.style.marginTop='12px';analysis.components.forEach((component,index)=>{const chip=document.createElement('span');chip.className='model-component-chip';chip.textContent=`C${index+1} · ${component.length} node${component.length===1?'':'s'}`;chip.title=component.map(id=>graph.nodes.find(node=>node.id===id)?.type??id).join(', ');components.appendChild(chip);});host.appendChild(components);
    const boundaries=document.createElement('div');boundaries.className='model-boundary-row';boundaries.innerHTML='<div class="model-boundary"><small>SOURCES</small><strong></strong></div><div class="model-boundary"><small>SINKS</small><strong></strong></div>';boundaries.children[0].querySelector('strong').textContent=String(analysis.sources.length);boundaries.children[0].title=analysis.sources.map(item=>item.node.type).join(', ')||'None';boundaries.children[1].querySelector('strong').textContent=String(analysis.sinks.length);boundaries.children[1].title=analysis.sinks.map(item=>item.node.type).join(', ')||'None';host.appendChild(boundaries);}
  function renderDegrees(analysis){const host=$('model-degree-list');host.textContent='';analysis.ranked.slice(0,5).forEach(item=>{const button=document.createElement('button');button.type='button';button.className='model-insight-node';button.innerHTML='<span><strong></strong><small></small></span><b></b>';button.querySelector('strong').textContent=item.node.label;button.querySelector('small').textContent=`${item.node.type} · ${item.incoming} in / ${item.outgoing} out`;button.querySelector('b').textContent=`degree ${item.total}`;button.addEventListener('click',()=>inspect(item.node.id));host.appendChild(button);});}
  function inspect(id){const target=document.querySelector(`.graph-node-button[data-node="${CSS.escape(id)}"]`);if(!target)return;target.click();target.scrollIntoView({behavior:'smooth',block:'center',inline:'center'});}
  install();let scheduled=false;const refresh=()=>{if(scheduled)return;scheduled=true;requestAnimationFrame(()=>{scheduled=false;render();});};new MutationObserver(refresh).observe($('graph-nodes'),{childList:true});window.addEventListener('storage',event=>{if(event.key===storageKey)refresh();});
})();