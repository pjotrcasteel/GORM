function enableReveal(){if(!('IntersectionObserver' in window)){document.querySelectorAll('.reveal').forEach(x=>x.classList.add('visible'));return;}const observer=new IntersectionObserver(entries=>entries.forEach(entry=>{if(entry.isIntersecting){entry.target.classList.add('visible');observer.unobserve(entry.target);}}),{threshold:.08});document.querySelectorAll('.reveal').forEach(x=>observer.observe(x));}

function enableDocsSearch(){const input=document.getElementById('docs-search');if(!input)return;const sections=[...document.querySelectorAll('.doc-section')];const empty=document.getElementById('search-empty');input.addEventListener('input',()=>{const query=input.value.trim().toLowerCase();let visible=0;sections.forEach(section=>{const text=`${section.dataset.search??''} ${section.textContent}`.toLowerCase();const match=!query||query.split(/\s+/).every(word=>text.includes(word));section.classList.toggle('search-hidden',!match);if(match)visible++;});if(empty)empty.hidden=visible!==0||!query;});}

const playgroundExamples={
  outgoing:`var projects = await context.Set<PersonNode>()\n    .Where(x => x.Id == personId)\n    .Outgoing<WorksOnEdge, ProjectNode>()\n    .ToListAsync();`,
  incoming:`var owners = await context.Set<ProjectNode>()\n    .Where(x => x.Id == projectId)\n    .Incoming<WorksOnEdge, PersonNode>()\n    .ToListAsync();`,
  filter:`var services = await context.Set<ServiceNode>()\n    .Where(x => x.State == ServiceState.Active)\n    .OrderBy(x => x.Name)\n    .Take(25)\n    .ToListAsync();`,
  temporal:`var snapshot = await context.Set<ServiceNode>()\n    .AsOf(incidentStartedAt)\n    .Where(x => x.Name == serviceName)\n    .ToListAsync();`,
  include:`var person = await context.Set<PersonNode>()\n    .Include(x => x.Projects)\n    .SingleAsync(x => x.Id == personId);`
};

function findRootType(query){return query.match(/Set\s*<\s*([A-Za-z_][A-Za-z0-9_.]*)\s*>/)?.[1]??null;}
function findTraversal(query,direction){const regex=direction==='outgoing'?/Outgoing\s*<\s*([A-Za-z_][A-Za-z0-9_.]*)\s*,\s*([A-Za-z_][A-Za-z0-9_.]*)\s*>/:/Incoming\s*<\s*([A-Za-z_][A-Za-z0-9_.]*)\s*,\s*([A-Za-z_][A-Za-z0-9_.]*)\s*>/;const match=query.match(regex);return match?{edge:match[1],other:match[2]}:null;}
function findTake(query){const match=query.match(/\.Take\s*\(\s*(\d+)\s*\)/);return match?Number(match[1]):null;}
function findAsOf(query){return query.match(/\.AsOf\s*\(\s*([A-Za-z_][A-Za-z0-9_.]*)\s*\)/)?.[1]??null;}
function findInclude(query){return query.match(/\.Include\s*\(\s*\w+\s*=>\s*\w+\.([A-Za-z_][A-Za-z0-9_]*)\s*\)/)?.[1]??null;}
function findPredicate(query){const id=query.match(/\.Where\s*\(\s*\w+\s*=>\s*\w+\.Id\s*==\s*([A-Za-z_][A-Za-z0-9_.]*)\s*\)/);if(id)return{column:'Id',parameter:id[1],kind:'parameter'};const name=query.match(/\.Where\s*\(\s*\w+\s*=>\s*\w+\.Name\s*==\s*([A-Za-z_][A-Za-z0-9_.]*)\s*\)/);if(name)return{column:'Name',parameter:name[1],kind:'parameter'};const state=query.match(/\.Where\s*\(\s*\w+\s*=>\s*\w+\.State\s*==\s*([A-Za-z_][A-Za-z0-9_.]*)\s*\)/);if(state)return{column:'State',parameter:state[1].split('.').at(-1),kind:'enum'};return null;}
function sqlParameter(predicate){if(!predicate)return null;return predicate.kind==='enum'?`@${predicate.column.toLowerCase()}`:`@${predicate.parameter}`;}

function translateGormQuery(query){const root=findRootType(query);if(!root)return{ok:false,sql:'-- Start with context.Set<TNode>() so the preview translator can identify the graph root.',notes:['No node-root Set<TNode>() expression was found.']};const outgoing=findTraversal(query,'outgoing');const incoming=findTraversal(query,'incoming');const take=findTake(query);const asOf=findAsOf(query);const include=findInclude(query);const predicate=findPredicate(query);const notes=[];let sql='';

  if(outgoing){const top=take?`TOP (${take}) `:'';sql=`SELECT ${top}target.*\nFROM [${root}] AS source,\n     [${outgoing.edge}] AS edge,\n     [${outgoing.other}] AS target\nWHERE MATCH(source-(edge)->target)`;if(predicate)sql+=`\n  AND source.[${predicate.column}] = ${sqlParameter(predicate)}`;if(asOf)sql+=`\n  -- temporal point: @${asOf}`;sql+=';';notes.push(`Outgoing traversal: ${root} → ${outgoing.edge} → ${outgoing.other}.`,'SQL Server Graph MATCH expresses the edge direction explicitly.');}
  else if(incoming){const top=take?`TOP (${take}) `:'';sql=`SELECT ${top}source.*\nFROM [${incoming.other}] AS source,\n     [${incoming.edge}] AS edge,\n     [${root}] AS target\nWHERE MATCH(source-(edge)->target)`;if(predicate)sql+=`\n  AND target.[${predicate.column}] = ${sqlParameter(predicate)}`;if(asOf)sql+=`\n  -- temporal point: @${asOf}`;sql+=';';notes.push(`Incoming traversal: ${incoming.other} → ${incoming.edge} → ${root}.`,'The selected rows are the source nodes that point at the current graph root.');}
  else if(include){sql=`-- Relationship-aware materialization\nSELECT node.*\nFROM [${root}] AS node`;if(predicate)sql+=`\nWHERE node.[${predicate.column}] = ${sqlParameter(predicate)}`;sql+=`;\n\n-- GORM relationship load for navigation: ${include}\n-- The migrated runtime translator determines the exact edge/table mapping.`;notes.push(`Include(${include}) requests relationship-aware object materialization.`,'Exact relationship SQL depends on model metadata, so this preview does not invent an edge type.');}
  else{const top=take?`TOP (${take}) `:'';sql=`SELECT ${top}node.*\nFROM [${root}] AS node`;if(asOf)sql+=`\nFOR SYSTEM_TIME AS OF @${asOf}`;if(predicate)sql+=`\nWHERE node.[${predicate.column}] = ${sqlParameter(predicate)}`;if(/OrderBy\s*\(\s*\w+\s*=>\s*\w+\.Name/.test(query))sql+='\nORDER BY node.[Name]';sql+=';';notes.push(`Node-root query over ${root}.`);if(asOf)notes.push(`Temporal query shape uses the supplied ${asOf} point in time.`);if(take)notes.push(`Take(${take}) maps to a TOP limit in this documentation preview.`);}

  if(predicate?.kind==='enum')notes.push(`Enum predicate ${predicate.parameter} is represented as a parameter; provider mapping decides the stored value.`);return{ok:true,sql,notes};}

function enablePlayground(){const editor=document.getElementById('query-editor');if(!editor)return;const output=document.querySelector('#sql-output code');const notes=document.getElementById('translation-notes');const status=document.getElementById('query-status');let active='outgoing';
  const setExample=key=>{active=key;editor.value=playgroundExamples[key];document.querySelectorAll('.preset').forEach(button=>button.classList.toggle('active',button.dataset.example===key));run();};
  const run=()=>{const result=translateGormQuery(editor.value);output.textContent=result.sql;notes.textContent='';result.notes.forEach((note,index)=>{const item=document.createElement('span');if(index===0){const strong=document.createElement('strong');strong.textContent=result.ok?'Translated: ':'Preview: ';item.append(strong);}item.append(document.createTextNode(note+(index<result.notes.length-1?' · ':'')));notes.append(item);});status.textContent=result.ok?'SQL shape generated':'Needs a supported GORM query shape';};
  document.querySelectorAll('.preset').forEach(button=>button.addEventListener('click',()=>setExample(button.dataset.example)));
  document.getElementById('run-query')?.addEventListener('click',run);
  document.getElementById('reset-query')?.addEventListener('click',()=>setExample(active));
  document.getElementById('copy-sql')?.addEventListener('click',async event=>{try{await navigator.clipboard.writeText(output.textContent);event.currentTarget.textContent='Copied';setTimeout(()=>event.currentTarget.textContent='Copy',1200);}catch{event.currentTarget.textContent='Select SQL';}});
  editor.addEventListener('keydown',event=>{if((event.ctrlKey||event.metaKey)&&event.key==='Enter'){event.preventDefault();run();}});
  editor.addEventListener('input',()=>status.textContent='Query changed — generate SQL to refresh');
  setExample(active);
}

enableReveal();enableDocsSearch();enablePlayground();
