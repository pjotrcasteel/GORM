(()=>{
  const draftKey='gorm-playground-draft';
  const examples={
    outgoing:`var projects = await context.Set<PersonNode>()\n    .Where(x => x.Id == personId)\n    .Outgoing<WorksOnEdge, ProjectNode>()\n    .ToListAsync();`,
    incoming:`var dependents = await context.Set<ServiceNode>()\n    .Where(x => x.Id == databaseId)\n    .Incoming<DependsOnEdge, ServiceNode>()\n    .ToListAsync();`,
    chained:`var databases = await context.Set<ServiceNode>()\n    .Where(x => x.Id == gatewayId)\n    .Outgoing<RoutesToEdge, ServiceNode>()\n    .Outgoing<DependsOnEdge, DatabaseNode>()\n    .ToListAsync();`,
    filter:`var services = await context.Set<ServiceNode>()\n    .Where(x => x.State == ServiceState.Active)\n    .OrderBy(x => x.Name)\n    .Skip(20)\n    .Take(25)\n    .ToListAsync();`,
    temporal:`var snapshot = await context.Set<ServiceNode>()\n    .AsOf(incidentStartedAt)\n    .Where(x => x.Name == serviceName)\n    .ToListAsync();`,
    include:`var person = await context.Set<PersonNode>()\n    .Include(x => x.Projects)\n    .SingleAsync(x => x.Id == personId);`
  };
  const $=id=>document.getElementById(id);

  function encodeQuery(value){
    const bytes=new TextEncoder().encode(value);let binary='';bytes.forEach(byte=>binary+=String.fromCharCode(byte));
    return btoa(binary).replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
  }
  function decodeQuery(value){
    try{
      const normalized=value.replace(/-/g,'+').replace(/_/g,'/');
      const padded=normalized+'='.repeat((4-normalized.length%4)%4);
      const binary=atob(padded);return new TextDecoder().decode(Uint8Array.from(binary,char=>char.charCodeAt(0)));
    }catch{return null;}
  }
  function playgroundUrl(query){const url=new URL('./playground.html',location.href);url.searchParams.set('q',encodeQuery(query));return url.href;}
  function setActive(key){document.querySelectorAll('.preset').forEach(button=>button.classList.toggle('active',button.dataset.example===key));}
  function setQuery(editor,value){editor.value=value;try{localStorage.setItem(draftKey,value);}catch{}}

  function bind(){
    const editor=$('query-editor');if(!editor)return;
    const shared=decodeQuery(new URLSearchParams(location.search).get('q')??'');
    const hashExample=location.hash.slice(1);
    let active=hashExample in examples?hashExample:'outgoing';
    const initial=shared||examples[active];
    setQuery(editor,initial);

    if(shared){setActive('');$('query-status').textContent='Shared query loaded';}
    else setActive(active);

    document.querySelectorAll('.preset').forEach(button=>button.addEventListener('click',()=>{
      const key=button.dataset.example;if(!(key in examples))return;active=key;setQuery(editor,examples[key]);setActive(key);
      const url=new URL(location.href);url.searchParams.delete('q');url.hash=key;history.replaceState(null,'',url);
    }));

    $('reset-query')?.addEventListener('click',()=>{setQuery(editor,examples[active]);setActive(active);});
    $('copy-sql')?.addEventListener('click',async event=>{
      const output=document.querySelector('#sql-output code')?.textContent??'';
      try{await navigator.clipboard.writeText(output);event.currentTarget.textContent='Copied';setTimeout(()=>event.currentTarget.textContent='Copy',1200);}
      catch{event.currentTarget.textContent='Select SQL';}
    });
    $('share-query')?.addEventListener('click',async event=>{
      const url=playgroundUrl(editor.value);
      try{await navigator.clipboard.writeText(url);event.currentTarget.textContent='Link copied';setTimeout(()=>event.currentTarget.textContent='Share query',1400);}
      catch{history.replaceState(null,'',url);event.currentTarget.textContent='URL updated';}
    });
    editor.addEventListener('input',()=>{
      $('query-status').textContent='Query changed — analyze to refresh';setActive('');
      try{localStorage.setItem(draftKey,editor.value);}catch{}
    });

    let saved=null;try{saved=localStorage.getItem(draftKey);}catch{}
    const restore=$('restore-query');
    if(!shared&&saved&&saved!==initial&&restore){
      restore.hidden=false;
      restore.addEventListener('click',()=>{setQuery(editor,saved);restore.hidden=true;setActive('');});
    }
  }

  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',bind,{once:true});else bind();
})();