(()=>{
  'use strict';

  // Full-match grammar for bounded mapped-property equality; no arbitrary C# is evaluated.
  const grammar=/^\s*var\s+results\s*=\s*await\s+context\.Set\s*<\s*(\w+)\s*>\s*\(\s*\)\s*\.Where\s*\(\s*x\s*=>\s*x\.(Name|State)\s*==\s*("[A-Za-z0-9 ._-]{1,64}"|ServiceState\.(?:Active|Inactive))\s*\)\s*\.OrderBy\s*\(\s*x\s*=>\s*x\.Name\s*\)\s*\.Skip\s*\(\s*(\d{1,5})\s*\)\s*\.Take\s*\(\s*(\d{1,3})\s*\)\s*\.ToListAsync\s*\(\s*\)\s*;\s*$/;
  const safeName=/^[A-Za-z0-9 ._-]{1,64}$/;
  let fields=[];
  const mapped=(root,property)=>fields.some(field=>field.root===root&&field.property===property);

  function parse(query){
    if(typeof query!=='string'||query.length>4096)return null;
    const match=grammar.exec(query);
    if(!match||!fields.length||!mapped(match[1],match[2]))return null;
    const value=match[3];
    if(match[2]==='Name'&&!(value.startsWith('"')&&value.endsWith('"')))return null;
    if(match[2]==='State'&&!/^ServiceState\.(Active|Inactive)$/.test(value))return null;
    const skip=Number(match[4]),take=Number(match[5]);
    if(skip>10000||take<1||take>100)return null;
    return {version:4,root:match[1],property:match[2],
      value:match[2]==='Name'?value.slice(1,-1):value.slice('ServiceState.'.length),
      orderBy:'Name',skip,take};
  }

  function format(intent){
    if(!intent||intent.version!==4||!mapped(intent.root,intent.property)||intent.orderBy!=='Name'
      ||!Number.isInteger(intent.skip)||intent.skip<0||intent.skip>10000
      ||!Number.isInteger(intent.take)||intent.take<1||intent.take>100){
      throw new RangeError('Select a mapped Name/State property with valid paging.');
    }
    if(intent.property==='Name'&&!safeName.test(intent.value))throw new RangeError('Name must be 1–64 letters, numbers, spaces, dots, hyphens or underscores.');
    if(intent.property==='State'&&!['Active','Inactive'].includes(intent.value))throw new RangeError('State must be Active or Inactive.');
    const literal=intent.property==='Name'?'"'+intent.value+'"':'ServiceState.'+intent.value;
    return 'var results = await context.Set<'+intent.root+'>()\n'
      +'    .Where(x => x.'+intent.property+' == '+literal+')\n'
      +'    .OrderBy(x => x.Name)\n'
      +'    .Skip('+intent.skip+')\n'
      +'    .Take('+intent.take+')\n'
      +'    .ToListAsync();';
  }

  function option(select,value,label){
    const o=document.createElement('option');o.value=value;o.textContent=label;select.append(o);
  }

  function setup(){
    const editor=document.getElementById('query-editor'),anchor=document.querySelector('.editor-grid');
    if(!editor||!anchor)return;
    const panel=document.createElement('section');
    panel.className='verified-filter-controls verified-predicate-controls';
    panel.setAttribute('aria-label','Model-aware GORM property filters');
    panel.innerHTML='<div><strong>Filter by a mapped property</strong><p id="predicate-help">Enable real GORM to discover supported properties. Help me fills in a sample query.</p></div>'
      +'<label>Node type <select id="predicate-root" disabled></select></label>'
      +'<label>Property <select id="predicate-property" disabled></select></label>'
      +'<label id="predicate-name-wrap">Name value <input id="predicate-value" maxlength="64" value="Billing API" disabled></label>'
      +'<label id="predicate-state-wrap" hidden>State value <select id="predicate-state" disabled><option value="Active">Active</option><option value="Inactive">Inactive</option></select></label>'
      +'<label>Skip <input id="predicate-skip" type="number" min="0" max="10000" value="0"></label>'
      +'<label>Take <input id="predicate-take" type="number" min="1" max="100" value="25"></label>'
      +'<button id="predicate-help-me" class="tiny-button playground-help-button" type="button" disabled>Help me · fill example</button>'
      +'<button id="predicate-apply" class="tiny-button" type="button" disabled>Analyze filter</button>'
      +'<span id="predicate-error" role="status" aria-live="polite"></span>';
    anchor.before(panel);
    const $=id=>panel.querySelector('#'+id);
    const root=$('predicate-root'),property=$('predicate-property');
    const error=$('predicate-error');
    const sampleValue={PersonNode:'Ada Lovelace',ProjectNode:'Migration',ServiceNode:'Billing API',DatabaseNode:'Orders'};
    function refreshProperties(preferred=null){
      property.textContent='';
      fields.filter(field=>field.root===root.value).forEach(field=>option(property,field.property,field.property));
      if(preferred&&mapped(root.value,preferred))property.value=preferred;
      const isState=property.value==='State';
      $('predicate-name-wrap').hidden=isState;
      $('predicate-state-wrap').hidden=!isState;
    }
    function sync(){
      const intent=parse(editor.value);
      if(!intent)return;
      root.value=intent.root;
      refreshProperties(intent.property);
      if(intent.property==='State')$('predicate-state').value=intent.value;
      else $('predicate-value').value=intent.value;
      $('predicate-skip').value=String(intent.skip);
      $('predicate-take').value=String(intent.take);
    }
    function fill(){
      // An actual mapped field and a known example, without changing the editor or triggering a run.
      const choice=fields.find(field=>field.root==='ServiceNode'&&field.property==='Name')??fields[0];
      if(!choice)return;
      root.value=choice.root;refreshProperties(choice.property);
      $('predicate-value').value=sampleValue[choice.root]??'Example';
      $('predicate-state').value='Active';
      $('predicate-skip').value='0';$('predicate-take').value='25';
      error.textContent='Example values inserted. Click Analyze filter to create verified GORM SQL.';
    }
    root.addEventListener('change',()=>refreshProperties());
    property.addEventListener('change',()=>refreshProperties(property.value));
    $('predicate-help-me').addEventListener('click',fill);
    $('predicate-apply').addEventListener('click',()=>{
      try{
        const intent={version:4,root:root.value,property:property.value,orderBy:'Name',
          value:property.value==='State'?$('predicate-state').value:$('predicate-value').value,
          skip:Number($('predicate-skip').value),take:Number($('predicate-take').value)};
        const query=format(intent);
        error.textContent='';
        editor.value=query;
        editor.dispatchEvent(new Event('input',{bubbles:true}));
        document.getElementById('run-query')?.click();
      }catch(e){error.textContent=e.message;}
    });
    editor.addEventListener('input',sync);
    document.querySelectorAll('.preset').forEach(button=>button.addEventListener('click',()=>queueMicrotask(sync)));
    document.addEventListener('gorm:runtime-ready',()=>{
      const catalog=window.GormPlaygroundEngine?.predicateCatalog;
      if(!Array.isArray(catalog)||!catalog.length)return;
      fields=catalog.filter(field=>field&&typeof field.root==='string'&&['Name','State'].includes(field.property));
      if(!fields.length)return;
      root.textContent='';
      [...new Set(fields.map(field=>field.root))].forEach(name=>option(root,name,name));
      root.disabled=false;property.disabled=false;
      $('predicate-value').disabled=false;$('predicate-state').disabled=false;
      $('predicate-help-me').disabled=false;$('predicate-apply').disabled=false;
      $('predicate-help').textContent='These fields come from real GORM model metadata; only supported equality shapes are translated.';
      refreshProperties();sync();
    });
  }
  window.GormPlaygroundPredicates={parse,format};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',setup,{once:true});
  else setup();
})();