(()=>{
  'use strict';

  // A single whole-query C# shape: two equality predicates joined by validated && (v5) or || (v6). Never evaluate editor C#.
  const grammar=/^\s*var\s+results\s*=\s*await\s+context\.Set\s*<\s*ServiceNode\s*>\s*\(\s*\)\s*\.Where\s*\(\s*x\s*=>\s*x\.Name\s*==\s*"([A-Za-z0-9 ._-]{1,64})"\s*(&&|\|\|)\s*x\.State\s*==\s*ServiceState\.(Active|Inactive)\s*\)\s*\.OrderBy\s*\(\s*x\s*=>\s*x\.Name\s*\)\s*\.Skip\s*\(\s*(\d{1,5})\s*\)\s*\.Take\s*\(\s*(\d{1,3})\s*\)\s*\.ToListAsync\s*\(\s*\)\s*;\s*$/;
  const safeName=/^[A-Za-z0-9 ._-]{1,64}$/;

  function mapped(){
    const fields=window.GormPlaygroundEngine?.predicateCatalog;
    return Array.isArray(fields)&&['Name','State'].every(property=>
      fields.some(field=>field.root==='ServiceNode'&&field.property===property));
  }

  function parse(query){
    if(typeof query!=='string'||query.length>4096||!mapped())return null;
    const match=grammar.exec(query);
    if(!match)return null;
    const skip=Number(match[4]),take=Number(match[5]);
    if(skip>10000||take<1||take>100)return null;
    const logic=match[2]==='&&'?'and':'or';
    return {version:logic==='and'?5:6,root:'ServiceNode',name:match[1],state:match[3],logic,orderBy:'Name',skip,take};
  }

  function format(intent){
    if(!intent||!mapped()||!((intent.version===5&&intent.logic==='and')||(intent.version===6&&intent.logic==='or'))
      ||intent.root!=='ServiceNode'||intent.orderBy!=='Name'
      ||typeof intent.name!=='string'||!safeName.test(intent.name)
      ||!['Active','Inactive'].includes(intent.state)
      ||!Number.isInteger(intent.skip)||intent.skip<0||intent.skip>10000
      ||!Number.isInteger(intent.take)||intent.take<1||intent.take>100){
      throw new RangeError('Select a mapped ServiceNode name, Active/Inactive state and paging window.');
    }
    const operator=intent.logic==='and'?'&&':'||';
    return 'var results = await context.Set<ServiceNode>()\n'
      +'    .Where(x => x.Name == "'+intent.name+'" '+operator+' x.State == ServiceState.'+intent.state+')\n'
      +'    .OrderBy(x => x.Name)\n'
      +'    .Skip('+intent.skip+')\n'
      +'    .Take('+intent.take+')\n'
      +'    .ToListAsync();';
  }

  function setup(){
    const editor=document.getElementById('query-editor'),anchor=document.querySelector('.editor-grid');
    if(!editor||!anchor)return;
    const panel=document.createElement('section');
    panel.className='verified-filter-controls verified-combined-controls';
    panel.setAttribute('aria-label','Combined GORM ServiceNode predicates');
    panel.innerHTML='<div><strong>Combine two service filters</strong>'
      +'<p id="combined-help">Enable real GORM SQL and choose AND (both match) or OR (either matches).</p></div>'
      +'<label>Service name <input id="combined-name" type="text" maxlength="64" disabled></label>'
      +'<label>Logic <select id="combined-logic" aria-label="Combine predicates with" disabled><option value="and">AND</option><option value="or">OR</option></select></label>'
      +'<label>Service state <select id="combined-state" disabled><option value="Active">Active</option><option value="Inactive">Inactive</option></select></label>'
      +'<label>Skip <input id="combined-skip" type="number" min="0" max="10000" value="0"></label>'
      +'<label>Take <input id="combined-take" type="number" min="1" max="100" value="25"></label>'
      +'<button id="combined-help-me" type="button" class="tiny-button playground-help-button" disabled>Help me · fill example</button>'
      +'<button id="combined-apply" type="button" class="tiny-button" disabled>Analyze combined filter</button>'
      +'<span id="combined-error" role="status" aria-live="polite"></span>';
    anchor.before(panel);
    const $=id=>panel.querySelector('#'+id);
    function sync(){
      const intent=parse(editor.value);
      if(!intent)return;
      $('combined-name').value=intent.name;
      $('combined-state').value=intent.state;
      $('combined-logic').value=intent.logic;
      $('combined-skip').value=String(intent.skip);
      $('combined-take').value=String(intent.take);
    }
    $('combined-help-me').addEventListener('click',()=>{
      if(!mapped())return;
      $('combined-name').value='Billing API';
      $('combined-state').value='Active';
      $('combined-skip').value='0';
      $('combined-take').value='25';
      $('combined-error').textContent='Example values filled. Click Analyze combined filter to generate SQL.';
    });
    $('combined-apply').addEventListener('click',()=>{
      try{
        const logic=$('combined-logic').value;
        const intent={version:logic==='and'?5:6,root:'ServiceNode',name:$('combined-name').value,
          state:$('combined-state').value,logic,orderBy:'Name',
          skip:Number($('combined-skip').value),take:Number($('combined-take').value)};
        const query=format(intent);
        $('combined-error').textContent='';
        editor.value=query;
        editor.dispatchEvent(new Event('input',{bubbles:true}));
        document.getElementById('run-query')?.click();
      }catch(error){$('combined-error').textContent=error.message;}
    });
    editor.addEventListener('input',sync);
    document.querySelectorAll('.preset').forEach(button=>button.addEventListener('click',()=>queueMicrotask(sync)));
    document.addEventListener('gorm:runtime-ready',()=>{
      if(!mapped())return;
      $('combined-name').disabled=false;
      $('combined-state').disabled=false;
      $('combined-logic').disabled=false;
      $('combined-help-me').disabled=false;
      $('combined-apply').disabled=false;
      $('combined-help').textContent='The Name and State fields are mapped by GORM. Select AND or OR, then click Help me for example values.';
      sync();
    });
  }

  window.GormPlaygroundCombined={parse,format};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',setup,{once:true});
  else setup();
})();