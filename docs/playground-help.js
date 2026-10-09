(()=>{
  'use strict';

  // Human-readable sample values for SQL explanation only. None are real database records.
  const defaultId='00000000-0000-0000-0000-000000000001';
  function addHelp(panelSelector,applyId,fill,ready=()=>true){
    const panel=document.querySelector(panelSelector);
    const apply=document.getElementById(applyId);
    if(!panel||!apply)return;
    const button=document.createElement('button');
    button.type='button';button.className='tiny-button playground-help-button';
    button.textContent='Help me · fill example';button.title='Fill valid demonstration values; does not execute a query';
    const hint=document.createElement('span');
    hint.className='playground-help-hint';hint.setAttribute('role','status');hint.setAttribute('aria-live','polite');
    apply.before(button);
    panel.append(hint);
    const refresh=()=>{button.disabled=!ready();};
    button.addEventListener('click',()=>{
      if(!ready())return;
      try{
        fill();
        hint.textContent='Example values filled (no database connected). Click the Analyze button when ready.';
      }catch(error){hint.textContent='Could not fill sample: '+error.message;}
    });
    document.addEventListener('gorm:runtime-ready',refresh);
    refresh();
  }

  function fillLegacyFilter(){
    document.getElementById('verified-filter-state').value='Active';
    document.getElementById('verified-filter-skip').value='0';
    document.getElementById('verified-filter-take').value='25';
  }

  function chooseRoute(catalog,preferred){
    return catalog.find(item=>preferred(item))??catalog[0];
  }

  function fillSingleHop(){
    const catalog=window.GormPlaygroundEngine.traversalCatalog;
    const route=chooseRoute(catalog,item=>item.root==='PersonNode'&&item.direction==='outgoing'&&item.edge==='WorksOnEdge');
    const root=document.getElementById('traversal-root');
    root.value=route.root;
    root.dispatchEvent(new Event('change',{bubbles:true}));
    document.getElementById('traversal-route').value=[route.root,route.direction,route.edge,route.target].join('|');
    document.getElementById('traversal-id').value=defaultId;
  }

  function fillTwoHop(){
    const catalog=window.GormPlaygroundEngine.pathCatalog;
    const route=chooseRoute(catalog,item=>item.root==='ServiceNode'
      &&item.hops[0].direction==='outgoing'&&item.hops[0].edge==='RoutesToEdge'
      &&item.hops[1].direction==='outgoing'&&item.hops[1].edge==='DependsOnEdge');
    const key=hop=>[hop.direction,hop.edge,hop.target].join('|');
    const root=document.getElementById('path-root');
    root.value=route.root;
    root.dispatchEvent(new Event('change',{bubbles:true}));
    const first=document.getElementById('path-first');
    first.value=key(route.hops[0]);
    first.dispatchEvent(new Event('change',{bubbles:true}));
    document.getElementById('path-second').value=key(route.hops[1]);
    document.getElementById('path-id').value=defaultId;
  }

  function setup(){
    addHelp('.verified-filter-controls:not(.verified-traversal-controls):not(.verified-path-controls):not(.verified-predicate-controls):not(.verified-combined-controls)',
      'verified-filter-apply',fillLegacyFilter);
    addHelp('.verified-traversal-controls','traversal-apply',fillSingleHop,
      ()=>Array.isArray(window.GormPlaygroundEngine?.traversalCatalog)
        &&!document.getElementById('traversal-apply')?.disabled);
    addHelp('.verified-path-controls','path-apply',fillTwoHop,
      ()=>Array.isArray(window.GormPlaygroundEngine?.pathCatalog)
        &&!document.getElementById('path-apply')?.disabled);
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',setup,{once:true});
  else setup();
})();