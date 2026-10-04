(()=>{
  window.GormPlaygroundExamples=[
    {
      key:'outgoing',
      id:'outgoing-one-hop',
      label:'Outgoing traversal',
      query:`var projects = await context.Set<PersonNode>()\n    .Where(x => x.Id == personId)\n    .Outgoing<WorksOnEdge, ProjectNode>()\n    .ToListAsync();`,
      expect:{root:'PersonNode',hops:1,result:'ProjectNode',sql:['MATCH','WorksOnEdge','@personId']}
    },
    {
      key:'incoming',
      id:'incoming-one-hop',
      label:'Incoming traversal',
      query:`var dependents = await context.Set<ServiceNode>()\n    .Where(x => x.Id == databaseId)\n    .Incoming<DependsOnEdge, ServiceNode>()\n    .ToListAsync();`,
      expect:{root:'ServiceNode',hops:1,result:'ServiceNode',sql:['MATCH','DependsOnEdge','@databaseId']}
    },
    {
      key:'chained',
      id:'two-hop-outgoing',
      label:'Two-hop traversal',
      query:`var databases = await context.Set<ServiceNode>()\n    .Where(x => x.Id == gatewayId)\n    .Outgoing<RoutesToEdge, ServiceNode>()\n    .Outgoing<DependsOnEdge, DatabaseNode>()\n    .ToListAsync();`,
      expect:{root:'ServiceNode',hops:2,result:'DatabaseNode',sql:['RoutesToEdge','DependsOnEdge','node2.*']}
    },
    {
      key:'filter',
      id:'filter-paging',
      label:'Filter + paging',
      query:`var services = await context.Set<ServiceNode>()\n    .Where(x => x.State == ServiceState.Active)\n    .OrderBy(x => x.Name)\n    .Skip(20)\n    .Take(25)\n    .ToListAsync();`,
      expect:{root:'ServiceNode',hops:0,result:'ServiceNode',sql:['ORDER BY','OFFSET 20 ROWS','FETCH NEXT 25 ROWS ONLY']}
    },
    {
      key:'temporal',
      id:'temporal-asof',
      label:'AsOf snapshot',
      query:`var snapshot = await context.Set<ServiceNode>()\n    .AsOf(incidentStartedAt)\n    .Where(x => x.Name == serviceName)\n    .ToListAsync();`,
      expect:{root:'ServiceNode',hops:0,result:'ServiceNode',temporal:true,sql:['FOR SYSTEM_TIME AS OF','@incidentStartedAt','@serviceName']}
    },
    {
      key:'include',
      id:'include-navigation',
      label:'Include relationship',
      query:`var person = await context.Set<PersonNode>()\n    .Include(x => x.Projects)\n    .SingleAsync(x => x.Id == personId);`,
      expect:{root:'PersonNode',hops:0,result:'PersonNode',include:'Projects',sql:['Relationship-aware materialization','Projects']}
    }
  ];
})();