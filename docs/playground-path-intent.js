(()=>{
  'use strict';

  // A full-match, two-hop C# shape. No eval, dynamic imports from user input or arbitrary C# compilation.
  const grammar=/^\s*var\s+nodeId\s*=\s*Guid\.Parse\s*\(\s*"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})"\s*\)\s*;\s*var\s+related\s*=\s*await\s+context\.Set\s*<\s*(\w+)\s*>\s*\(\s*\)\s*\.Where\s*\(\s*x\s*=>\s*x\.Id\s*==\s*nodeId\s*\)\s*\.(Outgoing|Incoming)\s*<\s*(\w+)\s*,\s*(\w+)\s*>\s*\(\s*\)\s*\.Then(Outgoing|Incoming)\s*<\s*(\w+)\s*,\s*(\w+)\s*>\s*\(\s*\)\s*\.ToListAsync\s*\(\s*\)\s*;\s*$/;
  const guid=/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
  const exampleId='00000000-0000-0000-0000-000000000001';
  let paths=[];

  const stepKey=step=>[step.direction,step.edge,step.target].join('|');
  const pathKey=path=>path.root+'|'+(path.hops||[]).map(stepKey).join('~');
  const mapped=path=>paths.some(item=>pathKey(item)===pathKey(path));

  function parse(query){
    if(typeof query!=='string'||query.length>4096)return null;
    const match=grammar.exec(query);
    if(!match)return null;
    const intent={version:3,nodeId:match[1].toLowerCase(),root:match[2],hops:[
      {direction:match[3].toLowerCase(),edge:match[4],target:match[5]},
      {direction:match[6].toLowerCase(),edge:match[7],target:match[8]}
    ]};
    return paths.length&&mapped(intent)?intent:null;
  }

  function format(intent){
    if(!intent||intent.version!==3||typeof intent.nodeId!=='string'||!guid.test(intent.nodeId)
      ||!Array.isArray(intent.hops)||intent.hops.length!==2||!paths.length||!mapped(intent)){
      throw new RangeError('Select a two-hop graph path mapped by GORM and a valid node GUID.');
    }
    const [first,second]=intent.hops,method=name=>name==='outgoing'?'Outgoing':'Incoming';
    return 'var nodeId = Guid.Parse("'+intent.nodeId.toLowerCase()+'");\n'
      +'var related = await context.Set<'+intent.root+'>()\n'
      +'    .Where(x => x.Id == nodeId)\n'
      +'    .'+method(first.direction)+'<'+first.edge+', '+first.target+'>()\n'
      +'    .Then'+method(second.direction)+'<'+second.edge+', '+second.target+'>()\n'
      +'    .ToListAsync();';
  }

  function option(select,value,label){
    const item=document.createElement('option');
    item.value=value;item.textContent=label;select.append(item);
  }

  function setup(){
    const anchor=document.querySelector('.editor-grid'),editor=document.getElementById('query-editor');
    if(!anchor||!editor)return;

    const panel=document.createElement('section');
    panel.className='verified-filter-controls verified-path-controls';
    panel.setAttribute('aria-label','Verified GORM two-hop graph exploration');
    panel.innerHTML='<div><strong>Two-hop graph paths <span class="playground-path-phase">v0.2.3</span></strong>'
      +'<p id="path-help">Enable real GORM SQL to compose two mapped relationship hops and inspect the actual MATCH query.</p></div>'
      +'<label>Start node <select id="path-root" aria-label="Two-hop root" disabled></select></label>'
      +'<label>First hop <select id="path-first" aria-label="First relationship hop" disabled></select></label>'
      +'<label>Second hop <select id="path-second" aria-label="Second relationship hop" disabled></select></label>'
      +'<label>Root GUID <input id="path-id" aria-label="Two-hop start node GUID" type="text" maxlength="36" value="'+exampleId+'" disabled></label>'
      +'<button class="tiny-button" id="path-apply" type="button" disabled>Analyze two-hop path</button>'
      +'<span id="path-error" role="status" aria-live="polite"></span>';
    anchor.before(panel);

    const root=panel.querySelector('#path-root'),first=panel.querySelector('#path-first');
    const second=panel.querySelector('#path-second'),id=panel.querySelector('#path-id');
    const button=panel.querySelector('#path-apply'),error=panel.querySelector('#path-error');

    const choices=()=>paths.filter(path=>path.root===root.value&&stepKey(path.hops[0])===first.value);

    function refreshSecond(preferred=null){
      second.textContent='';
      const available=choices();
      available.forEach(path=>option(second,stepKey(path.hops[1]),
        (path.hops[1].direction==='outgoing'?'→':'←')+' '+path.hops[1].edge+' → '+path.hops[1].target));
      if(preferred&&available.some(path=>stepKey(path.hops[1])===preferred))second.value=preferred;
    }

    function refreshFirst(preferred=null,preferredSecond=null){
      first.textContent='';
      const available=paths.filter(path=>path.root===root.value);
      const seen=new Set();
      for(const path of available){
        const key=stepKey(path.hops[0]);
        if(seen.has(key))continue;
        seen.add(key);
        option(first,key,(path.hops[0].direction==='outgoing'?'→':'←')+' '+path.hops[0].edge+' → '+path.hops[0].target);
      }
      if(preferred&&seen.has(preferred))first.value=preferred;
      refreshSecond(preferredSecond);
    }

    function sync(){
      const intent=parse(editor.value);
      if(!intent)return;
      root.value=intent.root;
      refreshFirst(stepKey(intent.hops[0]),stepKey(intent.hops[1]));
      id.value=intent.nodeId;
    }

    root.addEventListener('change',()=>refreshFirst());
    first.addEventListener('change',()=>refreshSecond());
    button.addEventListener('click',()=>{
      try{
        const path=choices().find(route=>stepKey(route.hops[1])===second.value);
        const query=format({...path,version:3,nodeId:id.value.trim()});
        error.textContent='';
        editor.value=query;
        editor.dispatchEvent(new Event('input',{bubbles:true}));
        document.getElementById('run-query')?.click();
      }catch(cause){error.textContent=cause.message;}
    });
    editor.addEventListener('input',sync);
    document.querySelectorAll('.preset').forEach(button=>button.addEventListener('click',()=>queueMicrotask(sync)));
    document.addEventListener('gorm:runtime-ready',()=>{
      const catalog=window.GormPlaygroundEngine?.pathCatalog;
      if(!Array.isArray(catalog)||!catalog.length)return;
      paths=catalog.filter(path=>typeof path?.root==='string'&&Array.isArray(path.hops)&&path.hops.length===2
        &&path.hops.every(hop=>typeof hop.direction==='string'&&typeof hop.edge==='string'&&typeof hop.target==='string'));
      if(!paths.length)return;
      root.textContent='';
      [...new Set(paths.map(path=>path.root))].forEach(name=>option(root,name,name));
      root.disabled=false;first.disabled=false;second.disabled=false;id.disabled=false;button.disabled=false;
      panel.querySelector('#path-help').textContent='Both hops are selected from real GORM model relationships. Unsupported C# remains illustrative.';
      refreshFirst();
      sync();
    });
  }

  window.GormPlaygroundPaths={parse,format};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',setup,{once:true});
  else setup();
})();