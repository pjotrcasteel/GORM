(()=>{
  'use strict';
  // Full-match C# subset, never executable input. The .NET host revalidates the exact model route.
  const grammar=/^\s*var\s+nodeId\s*=\s*Guid\.Parse\s*\(\s*"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})"\s*\)\s*;\s*var\s+related\s*=\s*await\s+context\.Set\s*<\s*(\w+)\s*>\s*\(\s*\)\s*\.Where\s*\(\s*x\s*=>\s*x\.Id\s*==\s*nodeId\s*\)\s*\.(Outgoing|Incoming)\s*<\s*(\w+)\s*,\s*(\w+)\s*>\s*\(\s*\)\s*\.ToListAsync\s*\(\s*\)\s*;\s*$/;
  const guid=/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
  const defaultId='00000000-0000-0000-0000-000000000001';
  let routes=[];
  const routeKey=route=>[route.root,route.direction,route.edge,route.target].join('|');
  const validRoute=route=>routes.some(candidate=>routeKey(candidate)===routeKey(route));

  function parse(query){
    if(typeof query!=='string'||query.length>4096)return null;
    const match=grammar.exec(query);
    if(!match)return null;
    const intent={version:2,nodeId:match[1].toLowerCase(),root:match[2],
      direction:match[3].toLowerCase(),edge:match[4],target:match[5]};
    return routes.length&&validRoute(intent)?intent:null;
  }

  function format(intent){
    if(!intent||intent.version!==2||typeof intent.nodeId!=='string'||!guid.test(intent.nodeId)
      ||!routes.length||!validRoute(intent))throw new RangeError('Select a relationship mapped by real GORM and a valid node GUID.');
    const operation=intent.direction==='outgoing'?'Outgoing':'Incoming';
    return 'var nodeId = Guid.Parse("'+intent.nodeId.toLowerCase()+'");\n'
      +'var related = await context.Set<'+intent.root+'>()\n'
      +'    .Where(x => x.Id == nodeId)\n'
      +'    .'+operation+'<'+intent.edge+', '+intent.target+'>()\n'
      +'    .ToListAsync();';
  }

  function addOption(select,value,label){
    const option=document.createElement('option');
    option.value=value;option.textContent=label;select.append(option);
  }

  function setupControls(){
    const editor=document.getElementById('query-editor'),anchor=document.querySelector('.editor-grid');
    if(!editor||!anchor)return;
    const panel=document.createElement('section');
    panel.className='verified-filter-controls verified-traversal-controls';
    panel.setAttribute('aria-label','Verified GORM graph traversal');
    panel.innerHTML='<div><strong>Explore mapped graph relationships</strong><p id="traversal-help">Enable real GORM SQL to discover the allowed nodes and edges from the live GORM model.</p></div>'
      +'<label>Starting node <select id="traversal-root" disabled aria-label="Graph root"></select></label>'
      +'<label>Relationship <select id="traversal-route" disabled aria-label="Graph relationship"></select></label>'
      +'<label>Node GUID <input id="traversal-id" type="text" value="'+defaultId+'" maxlength="36" disabled aria-label="Start node GUID"></label>'
      +'<button id="traversal-apply" type="button" class="tiny-button" disabled>Explore relationship</button>'
      +'<span id="traversal-error" role="status" aria-live="polite"></span>';
    anchor.before(panel);
    const root=panel.querySelector('#traversal-root'),relationship=panel.querySelector('#traversal-route');
    const id=panel.querySelector('#traversal-id'),apply=panel.querySelector('#traversal-apply'),error=panel.querySelector('#traversal-error');

    function refreshRelationships(preferred=null){
      relationship.textContent='';
      const choices=routes.filter(route=>route.root===root.value);
      choices.forEach(route=>addOption(relationship,routeKey(route),
        (route.direction==='outgoing'?'→':'←')+' '+route.edge+' → '+route.target));
      if(preferred&&choices.some(route=>routeKey(route)===preferred))relationship.value=preferred;
    }

    function sync(){
      const intent=parse(editor.value);
      if(!intent)return;
      root.value=intent.root;
      refreshRelationships(routeKey(intent));
      id.value=intent.nodeId;
    }

    apply.addEventListener('click',()=>{
      try{
        const route=routes.find(item=>routeKey(item)===relationship.value);
        const query=format({...route,version:2,nodeId:id.value.trim()});
        error.textContent='';
        editor.value=query;
        editor.dispatchEvent(new Event('input',{bubbles:true}));
        document.getElementById('run-query')?.click();
      }catch(cause){error.textContent=cause.message;}
    });
    root.addEventListener('change',()=>refreshRelationships());
    editor.addEventListener('input',sync);
    document.querySelectorAll('.preset').forEach(button=>button.addEventListener('click',()=>queueMicrotask(sync)));
    document.addEventListener('gorm:runtime-ready',()=>{
      const catalog=window.GormPlaygroundEngine?.traversalCatalog;
      if(!Array.isArray(catalog)||!catalog.length)return;
      routes=catalog.filter(route=>route&&typeof route.root==='string'&&typeof route.direction==='string'
        &&typeof route.edge==='string'&&typeof route.target==='string');
      if(!routes.length)return;
      root.textContent='';
      [...new Set(routes.map(route=>route.root))].forEach(name=>addOption(root,name,name));
      root.disabled=false;relationship.disabled=false;id.disabled=false;apply.disabled=false;
      panel.querySelector('#traversal-help').textContent='Choose a mapped node, traversal direction and edge. SQL comes from the real GORM provider, not a mock translator.';
      refreshRelationships();
      sync();
    });
  }

  window.GormPlaygroundTraversals={parse,format};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',setupControls,{once:true});
  else setupControls();
})();