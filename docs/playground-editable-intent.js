(()=>{
  'use strict';

  // Phase 2's first strictly parsed C# subset. Full-match grammar: never compile/eval arbitrary editor code.
  // The authoritative .NET runtime independently validates the versioned typed intent.
  const filter=/^\s*var\s+services\s*=\s*await\s+context\.Set\s*<\s*ServiceNode\s*>\s*\(\s*\)\s*\.Where\s*\(\s*x\s*=>\s*x\.State\s*==\s*ServiceState\.(Active|Inactive)\s*\)\s*\.OrderBy\s*\(\s*x\s*=>\s*x\.Name\s*\)\s*\.Skip\s*\(\s*(\d{1,5})\s*\)\s*\.Take\s*\(\s*(\d{1,3})\s*\)\s*\.ToListAsync\s*\(\s*\)\s*;\s*$/;

  function parse(query){
    if(typeof query!=='string'||query.length>4096)return null;
    const match=filter.exec(query);
    if(!match)return null;
    const skip=Number(match[2]),take=Number(match[3]);
    if(skip>10000||take<1||take>100)throw new RangeError('Verified GORM filter requires Skip 0–10000 and Take 1–100.');
    return {version:1,root:'ServiceNode',state:match[1],orderBy:'Name',skip,take};
  }

  function format(intent){
    if(intent?.version!==1||intent.root!=='ServiceNode'||!['Active','Inactive'].includes(intent.state)||intent.orderBy!=='Name'
      ||!Number.isInteger(intent.skip)||intent.skip<0||intent.skip>10000
      ||!Number.isInteger(intent.take)||intent.take<1||intent.take>100){
      throw new RangeError('Invalid bounded ServiceNode filter intent.');
    }
    return `var services = await context.Set<ServiceNode>()
    .Where(x => x.State == ServiceState.${intent.state})
    .OrderBy(x => x.Name)
    .Skip(${intent.skip})
    .Take(${intent.take})
    .ToListAsync();`;
  }

  function setupControls(){
    const editor=document.getElementById('query-editor');
    const anchor=document.querySelector('.editor-grid');
    if(!editor||!anchor)return;
    const panel=document.createElement('section');
    panel.className='verified-filter-controls';
    panel.setAttribute('aria-label','Verified editable GORM ServiceNode filter');
    panel.innerHTML=`<div><strong>Editable real-GORM query</strong><p>Choose a service state and page window. This generates a strictly validated C# subset; enable the .NET runtime above to see authoritative SQL.</p></div>
      <label>State <select id="verified-filter-state"><option value="Active">Active</option><option value="Inactive">Inactive</option></select></label>
      <label>Skip <input id="verified-filter-skip" type="number" min="0" max="10000" step="1" value="20"></label>
      <label>Take <input id="verified-filter-take" type="number" min="1" max="100" step="1" value="25"></label>
      <button id="verified-filter-apply" type="button" class="tiny-button">Apply to editor &amp; analyze</button>
      <span id="verified-filter-error" role="status" aria-live="polite"></span>`;
    anchor.before(panel);

    function sync(){
      let intent;
      try{intent=parse(editor.value);}catch{return;}
      if(!intent)return;
      panel.querySelector('#verified-filter-state').value=intent.state;
      panel.querySelector('#verified-filter-skip').value=String(intent.skip);
      panel.querySelector('#verified-filter-take').value=String(intent.take);
    }

    panel.querySelector('#verified-filter-apply').addEventListener('click',()=>{
      const state=panel.querySelector('#verified-filter-state').value;
      const skip=Number(panel.querySelector('#verified-filter-skip').value);
      const take=Number(panel.querySelector('#verified-filter-take').value);
      try{
        const query=format({version:1,root:'ServiceNode',state,orderBy:'Name',skip,take});
        panel.querySelector('#verified-filter-error').textContent='';
        editor.value=query;
        editor.dispatchEvent(new Event('input',{bubbles:true}));
        document.getElementById('run-query')?.click();
      }catch(error){panel.querySelector('#verified-filter-error').textContent=error.message;}
    });
    editor.addEventListener('input',sync);
    document.querySelectorAll('.preset').forEach(item=>item.addEventListener('click',()=>queueMicrotask(sync)));
    sync();
  }

  window.GormPlaygroundEditable={parse,format};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',setupControls,{once:true});
  else setupControls();
})();
