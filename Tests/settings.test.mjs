import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../Web/topposters.js', import.meta.url), 'utf8');
const { default: initialize } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
const nodes = new Map();
function node(id) {
    if (!nodes.has(id)) nodes.set(id, {
        value: '', checked: false, disabled: false, textContent: '', listeners: {},
        addEventListener(name, fn) { this.listeners[name] = fn; },
        querySelector(selector) { return node(selector); },
        reportValidity() { return true; }
    });
    return nodes.get(id);
}
const view = node('view');
let config = { ApiKey: 'TP-test', FutureOption: 'preserved' };
let tasks = [{ Id: 'task-id', Key: 'TopPostersRefreshArtwork', State: 'Idle' }];
let started = [];
let saves = 0;
let alerts = [];
globalThis.ApiClient = {
    async getPluginConfiguration() { return structuredClone(config); },
    async updatePluginConfiguration(_, value) { config = structuredClone(value); saves++; },
    async getScheduledTasks() { return tasks; },
    async startScheduledTask(id) { started.push(id); }
};
globalThis.Dashboard = {
    showLoadingMsg() {}, hideLoadingMsg() {}, processPluginConfigurationUpdateResult() {},
    alert(value) { alerts.push(value); }
};
initialize(view);
await view.listeners.viewshow();
assert.equal(node('#enablePosters').checked, true);
assert.equal(node('#enableSeasonPosters').checked, true);
assert.equal(node('#enableLogos').checked, false);
assert.equal(node('#enableArtworkRefresh').checked, false);
assert.equal(node('#refreshBatchSize').value, 100);

await node('#refreshArtwork').listeners.click();
assert.equal(started.length, 0);
assert.equal(saves, 0);
assert.match(node('#refreshStatus').textContent, /Turn on/);

node('#apiKey').value = ' TP-updated ';
node('#enableLogos').checked = true;
node('#enableArtworkRefresh').checked = true;
node('#refreshBatchSize').value = '25';
await node('#refreshArtwork').listeners.click();
assert.equal(config.ApiKey, 'TP-updated');
assert.equal(config.EnableLogos, true);
assert.equal(config.RefreshBatchSize, 25);
assert.equal(config.FutureOption, 'preserved');
assert.deepEqual(started, ['task-id']);
assert.match(node('#refreshStatus').textContent, /started/);

tasks[0].State = 'Running';
await node('#refreshArtwork').listeners.click();
assert.equal(started.length, 1);
assert.match(node('#refreshStatus').textContent, /already active/);
tasks = [];
await node('#refreshArtwork').listeners.click();
assert.match(node('#refreshStatus').textContent, /Restart Jellyfin/);

ApiClient.updatePluginConfiguration = async () => { throw new Error('server error'); };
await node('#TopPostersConfigForm').listeners.submit({ preventDefault() {} });
assert.equal(alerts.length, 1);
assert.equal(node('#refreshArtwork').disabled, false);
assert.equal(node('[type="submit"]').disabled, false);
console.log('Passed settings tests: upgrade defaults, opt-in, save/start, active task, missing task and error recovery.');
