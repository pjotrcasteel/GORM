(()=>{
  const adapter=window.GormPlaygroundAdapter;
  if(!adapter||!window.GormPreviewEngine)return;

  const engine=adapter.resolve(()=>window.GormPreviewEngine);
  let runVersion=0;

  function setText(id,value){const element=document.getElementById(id);if(element)element.textContent=value;}
  function renderEngineStatus(){
    const metadata=engine.metadata;
    setText('engine-name',metadata.label);
    setText('engine-version',metadata.version);
    setText('engine-execution',metadata.execution);
    setText('engine-authority',metadata.authoritative?'Authoritative runtime':'Documentation preview');
    const status=document.getElementById('engine-status');
    if(status)status.classList.toggle('authoritative',metadata.authoritative);
    const pill=document.querySelector('.preview-pill');
    if(pill)pill.textContent=metadata.authoritative?'GORM RUNTIME TRANSLATOR':'DOCS PREVIEW TRANSLATOR';
  }

  function renderResult(result){
    const output=document.querySelector('#sql-output code');
    const notes=document.getElementById('translation-notes');
    const status=document.getElementById('query-status');
    if(output)output.textContent=result.sql;
    if(notes){
      notes.textContent='';
      (result.notes??[]).forEach((note,index)=>{
        const item=document.createElement('span');
        if(index===0){const strong=document.createElement('strong');strong.textContent=result.ok?'Engine: ':'Translator: ';item.append(strong);}
        item.append(document.createTextNode(note+(index<(result.notes?.length??0)-1?' · ':'')));
        notes.append(item);
      });
    }
    if(status)status.textContent=result.ok?`${engine.metadata.label} refreshed`:'Translator needs a supported query root';
  }

  async function runEngine(){
    const editor=document.getElementById('query-editor');
    if(!editor)return;
    const version=++runVersion;
    try{
      const result=await adapter.translate(engine,editor.value);
      if(version!==runVersion)return;
      renderResult(result);
    }catch(error){
      if(version!==runVersion)return;
      const status=document.getElementById('query-status');
      if(status)status.textContent='Translator contract failed';
      const output=document.querySelector('#sql-output code');
      if(output)output.textContent=`-- ${error instanceof Error?error.message:String(error)}`;
    }
  }

  function contractFailure(contract,result){
    const expected=contract.expect;
    if(result.model.root!==expected.root)return `root ${result.model.root??'null'}`;
    if(result.model.traversals.length!==expected.hops)return `${result.model.traversals.length} hops`;
    const actualResult=result.model.traversals.at(-1)?.target??result.model.root;
    if(actualResult!==expected.result)return `result ${actualResult??'null'}`;
    if(Boolean(result.model.asOf)!==Boolean(expected.temporal))return 'temporal mismatch';
    if((result.model.include??null)!==(expected.include??null))return 'include mismatch';
    const missing=expected.sql.find(fragment=>!result.sql.includes(fragment));
    return missing?`SQL missing ${missing}`:null;
  }

  async function runContracts(){
    const contracts=window.GormPlaygroundContracts??[];
    const list=document.getElementById('contract-results');
    if(!list)return;
    list.textContent='';
    let passed=0;
    for(const contract of contracts){
      const row=document.createElement('div');
      const label=document.createElement('span');label.textContent=contract.label;
      const state=document.createElement('strong');
      try{
        const result=await adapter.translate(engine,contract.query);
        const failure=contractFailure(contract,result);
        if(failure){state.textContent=`FAIL · ${failure}`;row.className='contract-fail';}
        else{state.textContent='PASS';row.className='contract-pass';passed++;}
      }catch(error){state.textContent='FAIL · contract error';row.className='contract-fail';row.title=error instanceof Error?error.message:String(error);}
      row.append(label,state);list.appendChild(row);
    }
    setText('contract-summary',`${passed}/${contracts.length} examples compatible`);
  }

  function bind(){
    renderEngineStatus();
    const rerun=()=>queueMicrotask(runEngine);
    document.getElementById('run-query')?.addEventListener('click',rerun);
    document.getElementById('reset-query')?.addEventListener('click',rerun);
    document.getElementById('restore-query')?.addEventListener('click',rerun);
    document.querySelectorAll('.preset').forEach(button=>button.addEventListener('click',rerun));
    document.getElementById('query-editor')?.addEventListener('keydown',event=>{if((event.ctrlKey||event.metaKey)&&event.key==='Enter')rerun();});
    runEngine();
    runContracts();
  }

  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',bind,{once:true});else bind();
})();