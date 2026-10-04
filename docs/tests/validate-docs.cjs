const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const docsRoot = path.resolve(__dirname, '..');
const failures = [];
const files = fs.readdirSync(docsRoot, { withFileTypes: true });
const htmlFiles = files.filter(entry => entry.isFile() && entry.name.endsWith('.html')).map(entry => entry.name);
const jsFiles = files.filter(entry => entry.isFile() && entry.name.endsWith('.js')).map(entry => entry.name);

function fail(message) {
    failures.push(message);
    console.error(`FAIL ${message}`);
}

function validateJavaScript() {
    for (const file of jsFiles) {
        const result = spawnSync(process.execPath, ['--check', path.join(docsRoot, file)], { encoding: 'utf8' });
        if (result.status !== 0) fail(`${file}: JavaScript syntax error\n${result.stderr.trim()}`);
    }
    console.log(`Checked ${jsFiles.length} JavaScript files.`);
}

function localTarget(sourceFile, rawReference) {
    if (!rawReference || rawReference.startsWith('http:') || rawReference.startsWith('https:') || rawReference.startsWith('//') || rawReference.startsWith('mailto:') || rawReference.startsWith('tel:') || rawReference.startsWith('data:') || rawReference.startsWith('javascript:')) return null;

    const [rawPath, rawHash = ''] = rawReference.split('#', 2);
    const pathPart = rawPath.split('?', 1)[0];
    let target = pathPart ? path.resolve(path.dirname(sourceFile), decodeURIComponent(pathPart)) : sourceFile;
    if (target.endsWith(path.sep) || (fs.existsSync(target) && fs.statSync(target).isDirectory())) target = path.join(target, 'index.html');
    return { target, hash: decodeURIComponent(rawHash) };
}

function validateLinks() {
    const attributePattern = /(?:href|src)\s*=\s*(["'])(.*?)\1/g;
    for (const htmlName of htmlFiles) {
        const sourceFile = path.join(docsRoot, htmlName);
        const html = fs.readFileSync(sourceFile, 'utf8');
        for (const match of html.matchAll(attributePattern)) {
            const reference = match[2].trim();
            const local = localTarget(sourceFile, reference);
            if (!local) continue;
            if (!fs.existsSync(local.target)) {
                fail(`${htmlName}: missing local reference '${reference}'`);
                continue;
            }
            if (!local.hash || path.extname(local.target).toLowerCase() !== '.html') continue;
            const targetHtml = fs.readFileSync(local.target, 'utf8');
            const escapedHash = local.hash.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
            const anchorPattern = new RegExp(`(?:id|name)=["']${escapedHash}["']`);
            const playgroundPresetPattern = path.basename(local.target) === 'playground.html' ? new RegExp(`data-example=["']${escapedHash}["']`) : null;
            if (!anchorPattern.test(targetHtml) && !playgroundPresetPattern?.test(targetHtml)) fail(`${htmlName}: missing anchor or route '#${local.hash}' in ${path.basename(local.target)}`);
        }
    }
    console.log(`Checked local references in ${htmlFiles.length} HTML files.`);
}

async function validatePlaygroundContracts() {
    global.window = global;
    require(path.join(docsRoot, 'playground-preview-engine.js'));
    require(path.join(docsRoot, 'playground-adapter.js'));
    require(path.join(docsRoot, 'playground-contracts.js'));

    const adapter = global.GormPlaygroundAdapter;
    const engine = adapter.resolve(() => global.GormPreviewEngine);
    let passed = 0;

    for (const contract of global.GormPlaygroundContracts) {
        const result = await adapter.translate(engine, contract.query);
        const expected = contract.expect;
        const actualResult = result.model.traversals.at(-1)?.target ?? result.model.root;
        const issues = [];
        if (result.model.root !== expected.root) issues.push(`root=${result.model.root ?? 'null'}`);
        if (result.model.traversals.length !== expected.hops) issues.push(`hops=${result.model.traversals.length}`);
        if (actualResult !== expected.result) issues.push(`result=${actualResult ?? 'null'}`);
        if (Boolean(result.model.asOf) !== Boolean(expected.temporal)) issues.push('temporal mismatch');
        if ((result.model.include ?? null) !== (expected.include ?? null)) issues.push('include mismatch');
        for (const fragment of expected.sql) if (!result.sql.includes(fragment)) issues.push(`SQL missing '${fragment}'`);

        if (issues.length) fail(`playground contract ${contract.id}: ${issues.join(', ')}`);
        else {
            passed++;
            console.log(`PASS playground contract ${contract.id}`);
        }
    }
    console.log(`${passed}/${global.GormPlaygroundContracts.length} playground contracts passed.`);
}

(async () => {
    validateJavaScript();
    validateLinks();
    await validatePlaygroundContracts();
    if (failures.length) {
        console.error(`\nDocumentation validation failed with ${failures.length} issue(s).`);
        process.exit(1);
    }
    console.log('\nDocumentation validation passed.');
})().catch(error => {
    console.error(error);
    process.exit(1);
});