(()=>{
  const examples=window.GormPlaygroundExamples??[];
  window.GormPlaygroundContracts=examples.map(example=>({id:example.id,label:example.label,query:example.query,expect:example.expect}));
})();