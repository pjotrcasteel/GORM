const assert = require('node:assert/strict');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const { chromium } = require('playwright');

const root = path.resolve(process.argv[2] ?? '');
const reference = fs.readFileSync(process.argv[3], 'utf8');
const expected = new Map([...reference.matchAll(/^PASS (outgoing|incoming|chained|filter): (.*)$/gm)].map(match => [match[1], match[2]]));
const normalize = text => text.replace(/\s+/g, ' ').trim();
const intentCases = new Map([...reference.matchAll(/^PASS INTENT (\d+): ([A-Za-z0-9+/=]+)$/gm)]
  .map(match => [Number(match[1]), JSON.parse(Buffer.from(match[2], 'base64').toString('utf8'))]));
const traversalCases = new Map([...reference.matchAll(/^PASS TRAVERSAL (\d+): ([A-Za-z0-9+/=]+)$/gm)]
  .map(match => [Number(match[1]), JSON.parse(Buffer.from(match[2], 'base64').toString('utf8'))]));
const traversalCatalogLine = reference.match(/^PASS TRAVERSAL CATALOG: ([A-Za-z0-9+/=]+)$/m);
assert.ok(traversalCatalogLine, 'Native GORM must supply an actual mapped graph route catalog');
const nativeTraversalRoutes = JSON.parse(Buffer.from(traversalCatalogLine[1], 'base64').toString('utf8'));
assert.equal(nativeTraversalRoutes.length, 6, 'Playground must expose exactly the six mapped single-hop directions');
const pathCases = new Map([...reference.matchAll(/^PASS PATH (\d+): ([A-Za-z0-9+/=]+)$/gm)]
  .map(match => [Number(match[1]), JSON.parse(Buffer.from(match[2], 'base64').toString('utf8'))]));
const pathCatalogLine = reference.match(/^PASS PATH CATALOG: ([A-Za-z0-9+/=]+)$/m);
assert.ok(pathCatalogLine, 'Native GORM must supply mapped two-hop graph path catalog');
const nativePaths = JSON.parse(Buffer.from(pathCatalogLine[1], 'base64').toString('utf8'));
assert.equal(nativePaths.length, 4, 'Playground must expose exactly four validated two-hop routes');
const predicateCases = new Map([...reference.matchAll(/^PASS PREDICATE (\d+): ([A-Za-z0-9+/=]+)$/gm)]
  .map(match => [Number(match[1]), JSON.parse(Buffer.from(match[2], 'base64').toString('utf8'))]));
const predicateCatalogLine = reference.match(/^PASS PREDICATE CATALOG: ([A-Za-z0-9+/=]+)$/m);
assert.ok(predicateCatalogLine, 'Native GORM must expose its validated predicate model fields');
const nativePredicateFields = JSON.parse(Buffer.from(predicateCatalogLine[1], 'base64').toString('utf8'));
assert.equal(nativePredicateFields.length, 5, 'Mapped GORM property catalog should have four names and ServiceNode.State');
const combinedCases = new Map([...reference.matchAll(/^PASS COMBINED (\d+): ([A-Za-z0-9+/=]+)$/gm)]
  .map(match => [Number(match[1]), JSON.parse(Buffer.from(match[2], 'base64').toString('utf8'))]));
assert.equal(combinedCases.size, 120, 'Native GORM must produce 120 combined AND Explain results.');
assert.equal(predicateCases.size, 120, 'Native GORM must produce 120 mapped predicate Explain results.');
assert.equal(pathCases.size, 120, 'Native GORM must produce 120 independent two-hop Explain results.');
assert.equal(traversalCases.size, 120, 'Native GORM must produce 120 independent graph traversal results.');
assert.equal(intentCases.size, 120, 'Native GORM must produce 120 independent bounded intent results.');
assert.equal(expected.size, 4, 'Native GORM Explain() output must cover exactly four verified presets.');

const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json',
  '.wasm': 'application/wasm',
  '.dll': 'application/octet-stream',
  '.dat': 'application/octet-stream',
  '.pdb': 'application/octet-stream',
  '.svg': 'image/svg+xml'
};

const server = http.createServer((request, response) => {
  try {
    const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    const file = path.resolve(root, '.' + pathname);
    if (!file.startsWith(root + path.sep) || !fs.statSync(file).isFile()) {
      response.writeHead(404);
      response.end('Not found');
      return;
    }

    response.writeHead(200, { 'Content-Type': types[path.extname(file)] ?? 'application/octet-stream' });
    fs.createReadStream(file).pipe(response);
  } catch {
    response.writeHead(404);
    response.end('Not found');
  }
});

async function start() {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const diagnostics = [];
  page.on('pageerror', error => diagnostics.push('Page error: ' + error.message));
  page.on('console', message => {
    if (message.type() === 'error' || message.type() === 'warning') diagnostics.push('Console: ' + message.text());
  });

  try {
    const address = 'http://127.0.0.1:' + server.address().port + '/playground.html';
    await page.goto(address, { waitUntil: 'domcontentloaded' });
    await page.locator('#enable-real-gorm').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Verified GORM Explain()', null,
      { timeout: 90_000 });

    assert.match(await page.locator('#playground-release-id').textContent(), /Playground v0\.2\.5/);
    assert.match(await page.locator('#playground-release-id').textContent(), /GORM 3\.2\.0/);
    console.log('PASS independently versioned Playground v0.2.5 visible in site header');

    const testReal = async (key, table) => {
      await page.locator('[data-example="' + key + '"]').click();
      await page.waitForFunction(value => document.querySelector('#sql-output code')?.textContent.includes(value), table,
        { timeout: 30_000 });
      await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Verified GORM Explain()');
      const sql = await page.locator('#sql-output code').textContent();
      assert.equal(normalize(sql), normalize(expected.get(key)), key + ' browser SQL must equal native GORM Explain() SQL');
      console.log('PASS WASM ' + key + ' equals native GORM Explain()');
    };

    await testReal('outgoing', '[dbo].[Persons]');
    await testReal('incoming', '[dbo].[Databases]');
    await testReal('chained', '[dbo].[RoutesTo]');
    await testReal('filter', 'OFFSET 20 ROWS');

    for (const key of ['include', 'temporal']) {
      await page.locator('[data-example="' + key + '"]').click();
      await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Documentation preview');
      console.log('PASS ' + key + ' remains non-authoritative');
    }

    for (const [index, native] of intentCases) {
      const intent = {
        version: 1, root: 'ServiceNode', state: index % 2 === 0 ? 'Active' : 'Inactive',
        orderBy: 'Name', skip: index * 53 % 10001, take: index % 100 + 1
      };
      const actual = await page.evaluate(data => {
        const query = window.GormPlaygroundEditable.format(data);
        const result = window.GormPlaygroundEngine.translate(query);
        return {sql: result.sql, parameters: result.parameters};
      }, intent);
      assert.equal(actual.sql, native.sql, 'Editable intent ' + index + ' SQL must equal native provider bytes');
      assert.deepEqual(actual.parameters, native.parameters, 'Editable intent ' + index + ' parameter names and values must match native');
    }
    console.log('PASS 120 editable native/WASM intents with identical SQL and provider parameters');

    const catalog = await page.evaluate(() => window.GormPlaygroundEngine.traversalCatalog);
    assert.deepEqual(catalog, nativeTraversalRoutes, 'Browser model graph routes must equal native GORM metadata');
    for (const [index, native] of traversalCases) {
      const route = nativeTraversalRoutes[index % nativeTraversalRoutes.length];
      const nodeId = '00000000-0000-0000-0000-' + (index + 1).toString(16).padStart(12, '0');
      const intent = {...route, version: 2, nodeId};
      const actual = await page.evaluate(data => {
        const query = window.GormPlaygroundTraversals.format(data);
        const result = window.GormPlaygroundEngine.translate(query);
        return {sql: result.sql, parameters: result.parameters, authoritative: result.authoritative};
      }, intent);
      assert.equal(actual.authoritative, true, 'Model route ' + index + ' must be authoritative');
      assert.equal(actual.sql, native.sql, 'Route ' + index + ' SQL differs from native GORM Explain');
      assert.deepEqual(actual.parameters, native.parameters, 'Route ' + index + ' SQL parameters differ from native');
    }
    console.log('PASS 120 model-validated traversals with identical native/WASM SQL and parameters');

    await page.locator('#traversal-root').selectOption('DatabaseNode');
    await page.locator('#traversal-id').fill('00000000-0000-0000-0000-00000000000f');
    await page.locator('#traversal-apply').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Verified GORM Explain()');
    assert.match(await page.locator('#query-editor').inputValue(), /Incoming<DependsOnEdge, ServiceNode>/);
    assert.match(await page.locator('#sql-output code').textContent(), /MATCH/);
    assert.match(await page.locator('#anatomy-summary').textContent(), /1 graph hop/);
    console.log('PASS relationship selection updates editor, real MATCH SQL and graph anatomy');

    const invalidQuery = await page.locator('#query-editor').inputValue();
    await page.locator('#query-editor').fill(invalidQuery.replace('Incoming<DependsOnEdge, ServiceNode>', 'Outgoing<DependsOnEdge, ServiceNode>'));
    await page.locator('#run-query').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Documentation preview');
    console.log('PASS unmapped direction cannot become verified SQL');

    const pathCatalog = await page.evaluate(() => window.GormPlaygroundEngine.pathCatalog);
    assert.deepEqual(pathCatalog, nativePaths, 'Browser two-hop model catalog must equal native GORM routes');
    for (const [index, native] of pathCases) {
      const route = nativePaths[index % nativePaths.length];
      const id = '00000000-0000-0000-0000-' + (index + 31).toString(16).padStart(12, '0');
      const actual = await page.evaluate(intent => {
        const query = window.GormPlaygroundPaths.format(intent);
        const result = window.GormPlaygroundEngine.translate(query);
        return {sql: result.sql, parameters: result.parameters, authoritative: result.authoritative};
      }, {...route, version: 3, nodeId: id});
      assert.equal(actual.authoritative, true, 'Validated path ' + index + ' must be authoritative');
      assert.equal(actual.sql, native.sql, 'Native and browser two-hop SQL differ for path ' + index);
      assert.deepEqual(actual.parameters, native.parameters, 'Two-hop SQL parameters differ for path ' + index);
    }
    console.log('PASS 120 two-hop path variants match real native GORM SQL and parameters');

    await page.locator('#path-root').selectOption('DatabaseNode');
    await page.locator('#path-second').selectOption('outgoing|RoutesToEdge|ServiceNode');
    await page.locator('#path-id').fill('00000000-0000-0000-0000-000000000031');
    await page.locator('#path-apply').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Verified GORM Explain()');
    assert.match(await page.locator('#query-editor').inputValue(), /ThenOutgoing<RoutesToEdge, ServiceNode>/);
    assert.match(await page.locator('#sql-output code').textContent(), /MATCH/);
    assert.match(await page.locator('#anatomy-summary').textContent(), /2 graph hops/);
    console.log('PASS two-hop dropdowns produce verified GORM MATCH and graph-anatomy display');

    const corrupted = (await page.locator('#query-editor').inputValue())
      .replace('ThenOutgoing<RoutesToEdge, ServiceNode>', 'ThenIncoming<WorksOnEdge, PersonNode>');
    await page.locator('#query-editor').fill(corrupted);
    await page.locator('#run-query').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Documentation preview');
    console.log('PASS mismatched two-hop model route never becomes authoritative');

    const actualPredicateCatalog = await page.evaluate(() => window.GormPlaygroundEngine.predicateCatalog);
    assert.deepEqual(actualPredicateCatalog, nativePredicateFields, 'Browser predicate catalog must match native mapped GORM model');
    for (const [index, native] of predicateCases) {
      const field = nativePredicateFields[index % nativePredicateFields.length];
      const intent = {
        version: 4, root: field.root, property: field.property,
        value: field.property === 'State' ? (index % 2 === 0 ? 'Active' : 'Inactive') : 'Example ' + index,
        orderBy: 'Name', skip: index * 37 % 10001, take: index % 100 + 1
      };
      const actual = await page.evaluate(data => {
        const query = window.GormPlaygroundPredicates.format(data);
        const result = window.GormPlaygroundEngine.translate(query);
        return {sql: result.sql, parameters: result.parameters, authoritative: result.authoritative};
      }, intent);
      assert.equal(actual.authoritative, true, 'Mapped predicate must use authoritative GORM SQL: ' + index);
      assert.equal(actual.sql, native.sql, 'Native and WASM mapped predicate SQL differ for ' + index);
      assert.deepEqual(actual.parameters, native.parameters, 'Native and WASM mapped predicate values differ for ' + index);
    }
    console.log('PASS 120 model-aware Name/State predicate variants equal native GORM SQL and parameters');


    for (const [index, native] of combinedCases) {
      const intent = {
        version: 5, root: 'ServiceNode', name: 'Service ' + index,
        state: index % 2 === 0 ? 'Active' : 'Inactive', logic: 'and', orderBy: 'Name',
        skip: index * 71 % 10001, take: index % 100 + 1
      };
      const actual = await page.evaluate(data => {
        const query = window.GormPlaygroundCombined.format(data);
        const result = window.GormPlaygroundEngine.translate(query);
        return {sql: result.sql, parameters: result.parameters, authoritative: result.authoritative};
      }, intent);
      assert.equal(actual.authoritative, true, 'Combined AND query must be verified: ' + index);
      assert.equal(actual.sql, native.sql, 'Native and browser combined SQL disagree: ' + index);
      assert.deepEqual(actual.parameters, native.parameters, 'Native and browser combined parameters disagree: ' + index);
    }
    console.log('PASS 120 combined ServiceNode Name AND State real-GORM SQL and bound parameter parity cases');

    const beforeCombinedHelp = await page.locator('#query-editor').inputValue();
    await page.locator('#combined-help-me').click();
    assert.equal(await page.locator('#combined-name').inputValue(), 'Billing API');
    assert.equal(await page.locator('#combined-state').inputValue(), 'Active');
    assert.equal(await page.locator('#combined-skip').inputValue(), '0');
    assert.equal(await page.locator('#combined-take').inputValue(), '25');
    assert.equal(await page.locator('#query-editor').inputValue(), beforeCombinedHelp,
      'Combined Help me should populate only controls, without auto execution');
    await page.locator('#combined-apply').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Verified GORM Explain()');
    const combinedSql = await page.locator('#sql-output code').textContent();
    assert.match(combinedSql, / AND /i);
    const anatomy = await page.locator('#anatomy-flow').textContent();
    assert.match(anatomy, /Name == Billing API/);
    assert.match(anatomy, /State == Active/);
    assert.match(await page.locator('#translation-pipeline').textContent(), /Name == Billing API AND State == Active/);
    assert.match(await page.locator('#query-editor').inputValue(),
      /x.Name == "Billing API" && x.State == ServiceState.Active/);
    console.log('PASS combined Help me fills valid AND query, but only explicit Analyze executes it');

    await page.locator('#query-editor').fill((await page.locator('#query-editor').inputValue()).replace(' && ', ' || '));
    await page.locator('#run-query').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Documentation preview');
    console.log('PASS unsupported logical OR never becomes authoritative from composite parser');

    const beforeHelp = await page.locator('#query-editor').inputValue();
    await page.locator('#predicate-help-me').click();
    assert.equal(await page.locator('#predicate-root').inputValue(), 'ServiceNode');
    assert.equal(await page.locator('#predicate-property').inputValue(), 'Name');
    assert.equal(await page.locator('#predicate-value').inputValue(), 'Billing API');
    assert.equal(await page.locator('#query-editor').inputValue(), beforeHelp, 'Help me must never replace the editor on its own');
    await page.locator('#predicate-apply').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Verified GORM Explain()');
    assert.match(await page.locator('#sql-output code').textContent(), /SELECT/);
    assert.match(await page.locator('#query-editor').inputValue(), /x.Name == "Billing API"/);
    console.log('PASS guided mapped-predicate values require explicit Analyze click');

    await page.locator('#predicate-root').selectOption('DatabaseNode');
    await page.locator('#predicate-value').fill('Orders');
    await page.locator('#predicate-apply').click();
    await page.waitForFunction(() => document.querySelector('#sql-output code')?.textContent.includes('[dbo].[Databases]'));
    console.log('PASS model-aware Name predicate works for another mapped graph root');

    await page.locator('#predicate-root').selectOption('ServiceNode');
    await page.locator('#predicate-property').selectOption('State');
    await page.locator('#predicate-state').selectOption('Inactive');
    await page.locator('#predicate-apply').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Verified GORM Explain()');
    assert.match(await page.locator('#query-editor').inputValue(), /ServiceState.Inactive/);
    console.log('PASS model-aware enum State equality remains provider-verified');

    const beforeGuidedHop = await page.locator('#query-editor').inputValue();
    await page.locator('.verified-traversal-controls .playground-help-button').click();
    assert.equal(await page.locator('#traversal-root').inputValue(), 'PersonNode');
    assert.equal(await page.locator('#traversal-id').inputValue(), '00000000-0000-0000-0000-000000000001');
    assert.equal(await page.locator('#query-editor').inputValue(), beforeGuidedHop, 'One-hop Help me must not auto-execute');
    await page.locator('#traversal-apply').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Verified GORM Explain()');
    console.log('PASS one-hop Help me fills valid model route without auto-execution');

    const beforeGuidedPath = await page.locator('#query-editor').inputValue();
    await page.locator('.verified-path-controls .playground-help-button').click();
    assert.equal(await page.locator('#path-root').inputValue(), 'ServiceNode');
    assert.equal(await page.locator('#path-id').inputValue(), '00000000-0000-0000-0000-000000000001');
    assert.equal(await page.locator('#query-editor').inputValue(), beforeGuidedPath, 'Two-hop Help me must not auto-execute');
    await page.locator('#path-apply').click();
    await page.waitForFunction(() => document.querySelector('#anatomy-summary')?.textContent.includes('2 graph hops'));
    assert.match(await page.locator('#sql-output code').textContent(), /MATCH/);
    console.log('PASS two-hop Help me fills valid GORM route and requires explicit Analyze');

    await page.locator('#verified-filter-skip').fill('19');
    await page.locator('.verified-filter-controls:not(.verified-path-controls):not(.verified-traversal-controls):not(.verified-predicate-controls):not(.verified-combined-controls) .playground-help-button').click();
    assert.equal(await page.locator('#verified-filter-skip').inputValue(), '0');
    assert.equal(await page.locator('#verified-filter-take').inputValue(), '25');
    console.log('PASS original state filter Help me fills valid paging defaults');

    await page.locator('#verified-filter-state').selectOption('Inactive');
    await page.locator('#verified-filter-skip').fill('37');
    await page.locator('#verified-filter-take').fill('12');
    await page.locator('#verified-filter-apply').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Verified GORM Explain()');
    const edited = await page.locator('#sql-output code').textContent();
    assert.match(edited, /OFFSET 37 ROWS/);
    assert.match(edited, /FETCH NEXT 12 ROWS ONLY/);
    assert.match(await page.locator('#query-editor').inputValue(), /ServiceState.Inactive/);
    console.log('PASS editable controls use real GORM SQL and update editor');

    await page.locator('#query-editor').fill((await page.locator('#query-editor').inputValue()) + '\nSystem.IO.File.Delete("unsafe");');
    await page.locator('#run-query').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Documentation preview');
    console.log('PASS unsupported arbitrary C# is never authoritative');
    assert.deepEqual(diagnostics.filter(value => value.startsWith('Page error:')), [], diagnostics.join('\n'));
  } catch (error) {
    throw new Error(error.message + '\nBrowser diagnostics:\n' + diagnostics.join('\n'), { cause: error });
  } finally {
    await browser.close();
    await new Promise(resolve => server.close(resolve));
  }
}

start().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
