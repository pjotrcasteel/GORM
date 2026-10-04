(()=>{
  const savedKey='gorm-playground.saved-queries.v1';
  const historyKey='gorm-playground.history.v1';
  const maxSaved=50;
  const maxHistory=30;
  const $=id=>document.getElementById(id);

  function readArray(key){
    try{const value=JSON.parse(localStorage.getItem(key)??'[]');return Array.isArray(value)?value:[];}catch{return[];}
  }
  function writeArray(key,value){try{localStorage.setItem(key,JSON.stringify(value));}catch{}}
  function currentQuery(){return $('query-editor')?.value.trim()??'';}
  function catalogTitle(query){return(window.GormPlaygroundExamples??[]).find(example=>example.query.trim()===query)?.label??null;}
  function queryTitle(query){
    const catalog=catalogTitle(query);if(catalog)return catalog;
    const first=query.split('\n').map(line=>line.trim()).find(Boolean)??'Untitled query';
    return first.length>58?`${first.slice(0,57)}…`:first;
  }
  function formatTime(value){try{return new Date(value).toLocaleString();}catch{return'';}}
  function newId(){return crypto.randomUUID?.()??`${Date.now()}-${Math.random().toString(16).slice(2)}`;}
  function setStatus(message){const target=$('query-library-status');if(target)target.textContent=message;}

  function loadQuery(query){
    const editor=$('query-editor');if(!editor)return;editor.value=query;editor.dispatchEvent(new Event('input',{bubbles:true}));editor.focus();setOpen(false);setStatus('Query loaded into the editor.');
  }
  function openSave(){
    setOpen(true);const input=$('query-save-name');if(input&&!input.value)input.value=catalogTitle(currentQuery())??'';input?.focus();
  }
  function saveCurrent(){
    const query=currentQuery(),input=$('query-save-name');if(!query){setStatus('Nothing to save: the editor is empty.');return;}
    const suggested=catalogTitle(query)??queryTitle(query);const name=(input?.value.trim()||suggested).slice(0,60);if(!name){setStatus('Give the query a name first.');return;}
    const saved=readArray(savedKey);const existing=saved.find(item=>item.name.toLowerCase()===name.toLowerCase());
    if(existing){existing.query=query;existing.updatedAt=new Date().toISOString();}
    else saved.unshift({id:newId(),name,query,updatedAt:new Date().toISOString()});
    writeArray(savedKey,saved.slice(0,maxSaved));if(input)input.value='';render();setStatus(existing?`${name} updated.`:`${name} saved locally.`);
  }
  function deleteSaved(id){writeArray(savedKey,readArray(savedKey).filter(item=>item.id!==id));render();setStatus('Saved query removed.');}
  function saveHistoryQuery(item){
    const input=$('query-save-name');if(input)input.value=item.title;loadQuery(item.query);setOpen(true);setStatus('History query loaded. Name it and choose Save current query to keep it.');
  }
  function clearHistory(){if(!confirm('Clear all recent Playground runs from this browser?'))return;writeArray(historyKey,[]);render();setStatus('Recent run history cleared.');}
  function recordRun(detail){
    const query=String(detail?.query??'').trim();if(!query)return;
    const history=readArray(historyKey),now=new Date().toISOString();
    if(history[0]?.query===query){Object.assign(history[0],{ranAt:now,ok:Boolean(detail?.ok),engine:detail?.engine??null,title:queryTitle(query)});}
    else history.unshift({id:newId(),title:queryTitle(query),query,ranAt:now,ok:Boolean(detail?.ok),engine:detail?.engine??null});
    writeArray(historyKey,history.slice(0,maxHistory));render();
  }

  function itemButton(title,meta,onLoad){
    const button=document.createElement('button');button.type='button';button.className='query-library-item-main';
    const strong=document.createElement('strong');strong.textContent=title;const small=document.createElement('small');small.textContent=meta;button.append(strong,small);button.addEventListener('click',onLoad);return button;
  }
  function actionButton(label,onClick){const button=document.createElement('button');button.type='button';button.className='query-library-icon';button.textContent=label;button.addEventListener('click',onClick);return button;}
  function empty(host,text){const item=document.createElement('div');item.className='query-list-empty';item.textContent=text;host.appendChild(item);}
  function renderSaved(){
    const host=$('saved-query-list'),count=$('saved-query-count');if(!host||!count)return;const saved=readArray(savedKey);host.textContent='';count.textContent=String(saved.length);
    if(!saved.length){empty(host,'No saved queries yet. Save a useful traversal or diagnostic query here.');return;}
    saved.forEach(item=>{const row=document.createElement('div');row.className='query-library-item';row.append(itemButton(item.name,`Updated ${formatTime(item.updatedAt)}`,()=>loadQuery(item.query)));const actions=document.createElement('div');actions.className='query-library-item-actions';actions.append(actionButton('Load',()=>loadQuery(item.query)),actionButton('Delete',()=>deleteSaved(item.id)));row.appendChild(actions);host.appendChild(row);});
  }
  function renderHistory(){
    const host=$('query-history-list'),count=$('query-history-count');if(!host||!count)return;const history=readArray(historyKey);host.textContent='';count.textContent=`${history.length}/${maxHistory}`;
    if(!history.length){empty(host,'Queries appear here after the translator runs. Consecutive identical runs are combined.');return;}
    history.forEach(item=>{const row=document.createElement('div');row.className='query-library-item';const state=item.ok?'OK':'Needs attention';row.append(itemButton(item.title,`${state} · ${formatTime(item.ranAt)}`,()=>loadQuery(item.query)));const actions=document.createElement('div');actions.className='query-library-item-actions';actions.append(actionButton('Load',()=>loadQuery(item.query)),actionButton('Save',()=>saveHistoryQuery(item)));row.appendChild(actions);host.appendChild(row);});
  }
  function render(){renderSaved();renderHistory();}
  function setOpen(open){
    const panel=$('query-library'),button=$('query-library-toggle');if(!panel||!button)return;panel.hidden=!open;button.classList.toggle('active',open);button.setAttribute('aria-expanded',String(open));
  }

  function buildPanel(){
    const toolbar=document.querySelector('.playground-toolbar'),actions=document.querySelector('.playground-actions');if(!toolbar||!actions||$('query-library'))return;
    const style=document.createElement('link');style.rel='stylesheet';style.href='./playground-query-library.css';document.head.appendChild(style);
    const saveButton=document.createElement('button');saveButton.type='button';saveButton.className='tiny-button';saveButton.id='save-query';saveButton.textContent='Save query';saveButton.title='Save query (Ctrl/Cmd+S)';saveButton.addEventListener('click',openSave);
    const toggle=document.createElement('button');toggle.type='button';toggle.className='tiny-button query-library-toggle';toggle.id='query-library-toggle';toggle.textContent='Query library';toggle.setAttribute('aria-expanded','false');toggle.addEventListener('click',()=>setOpen($('query-library').hidden));actions.append(saveButton,toggle);

    const panel=document.createElement('section');panel.id='query-library';panel.className='query-library';panel.hidden=true;
    panel.innerHTML='<div class="query-library-head"><div><span>QUERY LIBRARY</span><strong>Saved queries & recent runs</strong></div><button id="query-library-close" type="button" class="tiny-button">Close</button></div><div class="query-save-row"><input id="query-save-name" maxlength="60" placeholder="Name this query" aria-label="Saved query name"><button id="save-current-query" type="button" class="tiny-button">Save current query</button></div><div class="query-library-grid"><article class="query-library-panel"><div class="query-library-panel-head"><div><span>SAVED</span><small id="saved-query-count">0</small></div></div><div id="saved-query-list" class="query-list"></div></article><article class="query-library-panel"><div class="query-library-panel-head"><div><span>RECENT RUNS</span><small id="query-history-count">0/30</small></div><button id="clear-query-history" type="button" class="query-library-icon">Clear</button></div><div id="query-history-list" class="query-list"></div></article></div><div id="query-library-status" class="query-library-status">Everything here is stored only in this browser.</div>';
    toolbar.insertAdjacentElement('afterend',panel);
    $('query-library-close').addEventListener('click',()=>setOpen(false));$('save-current-query').addEventListener('click',saveCurrent);$('clear-query-history').addEventListener('click',clearHistory);$('query-save-name').addEventListener('keydown',event=>{if(event.key==='Enter'){event.preventDefault();saveCurrent();}});render();
  }

  function bind(){
    buildPanel();document.addEventListener('gorm:playground-ran',event=>recordRun(event.detail));
    $('query-editor')?.addEventListener('keydown',event=>{if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='s'){event.preventDefault();openSave();}});
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',bind,{once:true});else bind();
})();