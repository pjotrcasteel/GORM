(()=>{
  const adapter=window.GormPlaygroundAdapter;
  if(!adapter||!window.GormPreviewEngine)return;

  let engine=adapter.resolve(()=>window.GormPreviewEngine);
  let runVersion=0;

  function setText(id,value){const element=document.getElementById(id);if(element)element.textContent=value;}
  function renderEngineStatus(authoritative=false){
    const metadata=engine.metadata;
    setText('engine-name',metadata.label);
    setText('engine-version',metadata.version);
    setText('engine-execution',metadata.execution);
    setText('engine-authority',authoritative?'Verified GORM Explain()':'Documentation preview');
    const status=document.getElementById('engine-status');
    if(status)status.classList.toggle('authoritative',authoritative);
    const pill=document.querySelector('.preview-pill');
    if(pill)pill.textContent=authoritative?'REAL GORM SQL · BROWSER WASM':'DOCS PREVIEW TRANSLATOR';
    const note=document.querySelector('.preview-note');
    if(note){
      const title=note.querySelector('strong');
      const text=note.querySelector('p');
      if(title)title.textContent=authoritative?'Verified GORM translator':'Documentation preview';
      if(text)text.textContent=authoritative?'This specific preset was translated using the real GORM Explain() running locally in .NET WebAssembly. The query is a bounded example; arbitrary C# edits still use a clearly identified preview.':'This query is being shown through the illustrative JavaScript translator. Only four unmodified, verified presets use real .NET GORM SQL; no remote execution or database connection is used.';
    }
  }

  function anatomyStep(kind,label,value){
    const step=document.createElement('div');
    step.className=`anatomy-step ${kind}`;
    const small=document.createElement('small');small.textContent=label;
    const strong=document.createElement('strong');strong.textContent=value;
    step.append(small,strong);
    return step;
  }

  function anatomyArrow(){const arrow=document.createElement('div');arrow.className='anatomy-arrow';arrow.textContent='→';return arrow;}

  function renderAnatomy(model){
    const flow=document.getElementById('anatomy-flow');
    const graph=document.getElementById('graph-preview');
    const metrics=document.getElementById('playground-metrics');
    const summary=document.getElementById('anatomy-summary');
    if(!flow||!graph||!metrics||!summary)return;

    flow.innerHTML='';graph.innerHTML='';metrics.innerHTML='';
    if(!model.root){summary.textContent='No graph root detected';flow.append(anatomyStep('predicate','NEEDS','Set<TNode>()'));return;}

    flow.append(anatomyStep('node','ROOT NODE',model.root));
    if(model.asOf)flow.append(anatomyArrow(),anatomyStep('predicate','TIME',`AsOf(${model.asOf})`));
    if(model.predicate)flow.append(anatomyArrow(),anatomyStep('predicate','PREDICATE',`${model.predicate.column??'value'} == ${model.predicate.display??'…'}`));

    let current=model.root;
    model.traversals.forEach(hop=>{
      flow.append(anatomyArrow(),anatomyStep('edge',hop.direction.toUpperCase(),hop.edge),anatomyArrow(),anatomyStep('node','NODE',hop.target));
      current=hop.target;
    });
    if(model.include)flow.append(anatomyArrow(),anatomyStep('edge','INCLUDE',model.include));
    if(model.order)flow.append(anatomyArrow(),anatomyStep('predicate','ORDER',model.order));
    if(model.skip!==null&&model.skip!==undefined||model.take!==null&&model.take!==undefined)flow.append(anatomyArrow(),anatomyStep('predicate','WINDOW',`Skip ${model.skip??0} / Take ${model.take??'∞'}`));

    summary.textContent=model.traversals.length?`${model.traversals.length} graph hop${model.traversals.length===1?'':'s'} · result ${current}`:model.include?`Relationship materialization · ${model.include}`:'Node-root query';

    const source=document.createElement('div');source.className='graph-node active';source.textContent=model.root;graph.appendChild(source);
    if(model.traversals.length){
      model.traversals.forEach(hop=>{
        const edge=document.createElement('div');edge.className=`graph-edge ${hop.direction==='incoming'?'reverse':''}`;
        const label=document.createElement('span');label.textContent=hop.edge;edge.appendChild(label);
        const target=document.createElement('div');target.className='graph-node';target.textContent=hop.target;
        graph.append(edge,target);
      });
    }else{
      const description=document.createElement('div');description.className='graph-description';description.textContent=model.include?`Materialize navigation: ${model.include}`:'No traversal: query stays on the root node set.';graph.appendChild(description);
    }

    const metricValues=[['ROOT',model.root],['GRAPH HOPS',String(model.traversals.length)],['TEMPORAL',model.asOf?'AsOf':'Current'],['RESULT',model.traversals.at(-1)?.target??model.root]];
    metricValues.forEach(([label,value])=>{
      const item=document.createElement('div');
      const small=document.createElement('small');small.textContent=label;
      const strong=document.createElement('strong');strong.textContent=value;
      item.append(small,strong);metrics.appendChild(item);
    });
  }

  function renderInspector(model){
    const pipeline=document.getElementById('translation-pipeline');
    const diagnostics=document.getElementById('query-diagnostics');
    if(!pipeline||!diagnostics)return;
    pipeline.innerHTML='';diagnostics.innerHTML='';

    const stages=[
      ['1','Expression root',model.root?`Resolve GraphSet<${model.root}>`:'Graph root missing'],
      ['2','Query scope',model.asOf?`Apply temporal point ${model.asOf}`:'Use current graph state'],
      ['3','Scalar predicates',model.predicate?`${model.predicate.column??'value'} == ${model.predicate.display??'…'}`:'No supported root predicate detected'],
      ['4','Graph operation',model.traversals.length?`${model.traversals.length} typed traversal hop${model.traversals.length===1?'':'s'}`:model.include?`Materialize ${model.include}`:'Stay on node root'],
      ['5','Result shaping',[model.order&&`OrderBy(${model.order})`,model.skip!==null&&model.skip!==undefined&&`Skip(${model.skip})`,model.take!==null&&model.take!==undefined&&`Take(${model.take})`].filter(Boolean).join(' · ')||'No paging/order operators'],
      ['6','Provider shape',model.traversals.length?'SQL Server Graph MATCH':'Node-table SELECT']
    ];
    stages.forEach(([number,title,text])=>{
      const row=document.createElement('div');row.className='pipeline-stage';
      const badge=document.createElement('b');badge.textContent=number;
      const body=document.createElement('div');
      const strong=document.createElement('strong');strong.textContent=title;
      const span=document.createElement('span');span.textContent=text;
      body.append(strong,span);row.append(badge,body);pipeline.appendChild(row);
    });

    const parameters=[];
    if(model.predicate?.parameter)parameters.push([`@${model.predicate.parameter}`,model.predicate.display??model.predicate.parameter,'Predicate']);
    if(model.asOf)parameters.push([`@${model.asOf}`,model.asOf,'Temporal point']);
    if(parameters.length){
      const table=document.createElement('div');table.className='parameter-list';
      parameters.forEach(([name,value,kind])=>{
        const row=document.createElement('div');
        const code=document.createElement('code');code.textContent=name;
        const span=document.createElement('span');span.textContent=value;
        const small=document.createElement('small');small.textContent=kind;
        row.append(code,span,small);table.appendChild(row);
      });
      diagnostics.appendChild(table);
    }else{
      const empty=document.createElement('p');empty.textContent='No bind parameters detected in the translator model.';diagnostics.appendChild(empty);
    }

    const warnings=[];
    if(model.traversals.length&&!model.predicate)warnings.push('Traversal has no supported root predicate; review potential fan-out.');
    if(model.skip!==null&&model.skip!==undefined&&!model.order)warnings.push('Skip without deterministic ordering can produce unstable pages.');
    if(model.include)warnings.push('Relationship materialization depends on model metadata supplied by the translator.');
    if(model.asOf)warnings.push('Temporal SQL in the Playground is illustrative and not a verified GORM SQL query.');
    if(!warnings.length)warnings.push('No obvious translator-level query warnings detected.');
    const list=document.createElement('div');list.className='diagnostic-list';
    warnings.forEach((warning,index)=>{const item=document.createElement('div');item.className=index===warnings.length-1&&warnings.length===1?'ok':'';item.textContent=warning;list.appendChild(item);});
    diagnostics.appendChild(list);
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
    renderAnatomy(result.model);
    renderInspector(result.model);
    renderEngineStatus(result.authoritative===true);
    if(status)status.textContent=result.ok?(result.authoritative===true?'Verified GORM Explain() SQL':'Documentation preview SQL'):'Translator needs a supported GORM query root';
  }

  function publishRun(query,ok,error=null){
    document.dispatchEvent(new CustomEvent('gorm:playground-ran',{detail:{query,ok,engine:engine.metadata.id,error:error?String(error):null}}));
  }

  async function runEngine(){
    const editor=document.getElementById('query-editor');
    if(!editor)return;
    const version=++runVersion;
    const query=editor.value;
    const status=document.getElementById('query-status');
    if(status)status.textContent=`Running ${engine.metadata.label}…`;
    try{
      const result=await adapter.translate(engine,query);
      if(version!==runVersion)return;
      renderResult(result);
      publishRun(query,result.ok);
    }catch(error){
      if(version!==runVersion)return;
      if(status)status.textContent='Translator contract failed';
      const output=document.querySelector('#sql-output code');
      if(output)output.textContent=`-- ${error instanceof Error?error.message:String(error)}`;
      publishRun(query,false,error instanceof Error?error.message:String(error));
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
    const expectedSql=result.authoritative===true?(expected.hops?['SELECT','MATCH']:['SELECT','ORDER BY','OFFSET']):expected.sql;
    const missing=expectedSql.find(fragment=>!result.sql.includes(fragment));
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

  document.addEventListener('gorm:runtime-ready',()=>{
    engine=adapter.resolve(()=>window.GormPreviewEngine);
    renderEngineStatus(false);
    void runEngine();
    void runContracts();
  });

  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',bind,{once:true});else bind();
})();