const assert = require('node:assert/strict');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const { chromium } = require('playwright');

const root = path.resolve(process.argv[2] ?? '');
const reference = fs.readFileSync(process.argv[3], 'utf8');
const expected = new Map([...reference.matchAll(/^PASS (outgoing|incoming|chained|filter): (.*)$/gm)].map(match => [match[1], match[2]]));
const normalize = text => text.replace(/\s+/g, ' ').trim();
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
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Verified GORM Explain()', null,
      { timeout: 90_000 });

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

    await page.locator('[data-example="filter"]').click();
    await page.locator('#query-editor').fill((await page.locator('#query-editor').inputValue()).replace('Take(25)', 'Take(24)'));
    await page.locator('#run-query').click();
    await page.waitForFunction(() => document.querySelector('#engine-authority')?.textContent === 'Documentation preview');
    console.log('PASS edited C# cannot be silently advertised as verified GORM SQL');
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
