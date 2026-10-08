(()=>{
  function findRootType(query){return query.match(/Set\s*<\s*([A-Za-z_][A-Za-z0-9_.]*)\s*>/)?.[1]??null;}
  function findTraversals(query){return [...query.matchAll(/\.(?:Then)?(Outgoing|Incoming)\s*<\s*([A-Za-z_][A-Za-z0-9_.]*)\s*,\s*([A-Za-z_][A-Za-z0-9_.]*)\s*>\s*\(\s*\)/g)].map(x=>({direction:x[1].toLowerCase(),edge:x[2],target:x[3]}));}
  function findTake(query){const match=query.match(/\.Take\s*\(\s*(\d+)\s*\)/);return match?Number(match[1]):null;}
  function findSkip(query){const match=query.match(/\.Skip\s*\(\s*(\d+)\s*\)/);return match?Number(match[1]):null;}
  function findAsOf(query){return query.match(/\.AsOf\s*\(\s*([A-Za-z_][A-Za-z0-9_.]*)\s*\)/)?.[1]??null;}
  function findInclude(query){return query.match(/\.Include\s*\(\s*\w+\s*=>\s*\w+\.([A-Za-z_][A-Za-z0-9_]*)\s*\)/)?.[1]??null;}
  function findOrder(query){return query.match(/\.OrderBy\s*\(\s*\w+\s*=>\s*\w+\.([A-Za-z_][A-Za-z0-9_]*)\s*\)/)?.[1]??null;}
  function findPredicate(query){const match=query.match(/\.Where\s*\(\s*\w+\s*=>\s*\w+\.(Id|Name|State|Region)\s*==\s*([^\)]+)\s*\)/);if(!match)return null;const value=match[2].trim();if(/^".*"$/.test(value))return{column:match[1],display:value,sql:value,parameter:null};if(value.includes('.'))return{column:match[1],display:value,sql:`@${match[1].toLowerCase()}`,parameter:match[1].toLowerCase()};return{column:match[1],display:value,sql:`@${value}`,parameter:value};}
  function table(type,alias,asOf){return `[${type}]${asOf?` FOR SYSTEM_TIME AS OF @${asOf}`:''} AS ${alias}`;}
  function analyze(query){return{root:findRootType(query),traversals:findTraversals(query),predicate:findPredicate(query),take:findTake(query),skip:findSkip(query),asOf:findAsOf(query),include:findInclude(query),order:findOrder(query)};}

  function translate(query){
    const model=analyze(query);
    if(!model.root)return{ok:false,sql:'-- Start with context.Set<TNode>() so the preview translator can identify the graph root.',notes:['No node-root Set<TNode>() expression was found.'],model};

    const notes=[];
    let sql='';
    if(model.traversals.length){
      const tables=[table(model.root,'node0',model.asOf)];
      const matches=[];
      model.traversals.forEach((hop,index)=>{
        tables.push(table(hop.edge,`edge${index}`,model.asOf),table(hop.target,`node${index+1}`,model.asOf));
        matches.push(hop.direction==='outgoing'?`MATCH(node${index}-(edge${index})->node${index+1})`:`MATCH(node${index+1}-(edge${index})->node${index})`);
      });
      const last=`node${model.traversals.length}`;
      const top=model.take&&!model.skip?`TOP (${model.take}) `:'';
      sql=`SELECT ${top}${last}.*\nFROM ${tables.join(',\n     ')}\nWHERE ${matches.join('\n  AND ')}`;
      if(model.predicate)sql+=`\n  AND node0.[${model.predicate.column}] = ${model.predicate.sql}`;
      if(model.order)sql+=`\nORDER BY ${last}.[${model.order}]`;
      if(model.skip!==null)sql+=`\nOFFSET ${model.skip} ROWS`;
      if(model.take&&model.skip!==null)sql+=`\nFETCH NEXT ${model.take} ROWS ONLY`;
      sql+=';';
      notes.push(`${model.traversals.length}-hop graph traversal from ${model.root} to ${model.traversals.at(-1).target}.`);
      notes.push('Each edge direction is represented explicitly with SQL Server Graph MATCH.');
      if(model.asOf)notes.push(`The preview applies the same @${model.asOf} point to participating node and edge tables.`);
    }else if(model.include){
      sql=`-- Relationship-aware materialization\nSELECT node.*\nFROM ${table(model.root,'node',model.asOf)}`;
      if(model.predicate)sql+=`\nWHERE node.[${model.predicate.column}] = ${model.predicate.sql}`;
      sql+=`;\n\n-- Load navigation: ${model.include}\n-- Exact edge mapping comes from GORM model metadata.`;
      notes.push(`Include(${model.include}) requests relationship-aware object materialization.`);
      notes.push('This preview does not invent an edge type when model metadata is unavailable.');
    }else{
      const top=model.take&&!model.skip?`TOP (${model.take}) `:'';
      sql=`SELECT ${top}node.*\nFROM ${table(model.root,'node',model.asOf)}`;
      if(model.predicate)sql+=`\nWHERE node.[${model.predicate.column}] = ${model.predicate.sql}`;
      if(model.order)sql+=`\nORDER BY node.[${model.order}]`;
      if(model.skip!==null)sql+=`\nOFFSET ${model.skip} ROWS`;
      if(model.take&&model.skip!==null)sql+=`\nFETCH NEXT ${model.take} ROWS ONLY`;
      sql+=';';
      notes.push(`Node-root query over ${model.root}.`);
      if(model.asOf)notes.push(`Temporal context uses @${model.asOf}.`);
      if(model.skip!==null||model.take)notes.push(`Paging shape: skip ${model.skip??0}, take ${model.take??'unbounded'}.`);
    }
    return{ok:true,sql,notes,model};
  }

  window.GormPreviewEngine={
    metadata:{id:'preview-js',label:'Documentation preview',version:'1',kind:'preview',authoritative:false,execution:'browser'},
    translate
  };
})();