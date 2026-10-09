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

    assert.match(await page.locator('#playground-release-id').textContent(), /Playground v0\.2\.3/);
    assert.match(await page.locator('#playground-release-id').textContent(), /GORM 3\.2\.0/);
    console.log('PASS independently versioned Playground v0.2.3 visible in site header');

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
