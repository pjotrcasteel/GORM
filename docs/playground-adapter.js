(()=>{
  const requiredResultFields=['ok','sql','model'];

  function normalizeMetadata(metadata={}){
    return {
      id:metadata.id??'unknown',
      label:metadata.label??'Unknown translator',
      version:metadata.version??'unversioned',
      kind:metadata.kind??'unknown',
      authoritative:metadata.authoritative===true,
      execution:metadata.execution??'browser'
    };
  }

  function validateEngine(engine){
    const errors=[];
    if(!engine||typeof engine!=='object')errors.push('Engine must be an object.');
    if(typeof engine?.translate!=='function')errors.push('Engine must expose translate(query).');
    return {ok:errors.length===0,errors};
  }

  function validateModel(model){
    const errors=[];
    if(!model||typeof model!=='object')return ["Translator result 'model' must be an object."];
    if(model.root!==null&&model.root!==undefined&&typeof model.root!=='string')errors.push("Model 'root' must be a string or null.");
    if(!Array.isArray(model.traversals))errors.push("Model 'traversals' must be an array.");
    else model.traversals.forEach((hop,index)=>{
      if(!hop||typeof hop!=='object')errors.push(`Traversal ${index} must be an object.`);
      else{
        if(hop.direction!=='outgoing'&&hop.direction!=='incoming')errors.push(`Traversal ${index} has invalid direction.`);
        if(typeof hop.edge!=='string'||!hop.edge)errors.push(`Traversal ${index} must provide an edge type.`);
        if(typeof hop.target!=='string'||!hop.target)errors.push(`Traversal ${index} must provide a target type.`);
      }
    });
    return errors;
  }

  function validateResult(result){
    const errors=[];
    if(!result||typeof result!=='object')return {ok:false,errors:['Translator returned no result object.']};
    requiredResultFields.forEach(field=>{if(!(field in result))errors.push(`Translator result is missing '${field}'.`);});
    if(typeof result.ok!=='boolean')errors.push("Translator result 'ok' must be a boolean.");
    if(typeof result.sql!=='string')errors.push("Translator result 'sql' must be a string.");
    if(result.notes!==undefined&&(!Array.isArray(result.notes)||result.notes.some(note=>typeof note!=='string')))errors.push("Translator result 'notes' must be an array of strings when provided.");
    errors.push(...validateModel(result.model));
    return {ok:errors.length===0,errors};
  }

  function resolve(fallbackFactory){
    const external=window.GormPlaygroundEngine;
    const externalValidation=validateEngine(external);
    if(externalValidation.ok)return {...external,metadata:normalizeMetadata(external.metadata)};

    const fallback=fallbackFactory();
    const fallbackValidation=validateEngine(fallback);
    if(!fallbackValidation.ok)throw new Error(`Invalid fallback translator: ${fallbackValidation.errors.join(' ')}`);
    return {...fallback,metadata:normalizeMetadata(fallback.metadata)};
  }

  async function translate(engine,query){
    const result=await Promise.resolve(engine.translate(query));
    const validation=validateResult(result);
    if(!validation.ok)throw new Error(validation.errors.join(' '));
    return result;
  }

  window.GormPlaygroundAdapter={resolve,translate,validateEngine,validateResult};
})();