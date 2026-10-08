import test from 'node:test';
import assert from 'node:assert/strict';
import { createCameraSession } from '../lib/camera-session.ts';
const fakeStream = () => {
  const tracks = [{ stopped: 0, stop() { this.stopped++; } }, { stopped: 0, stop() { this.stopped++; } }];
  return { getTracks: () => tracks, tracks };
};
test('camera does not acquire anything until explicitly requested; stop releases every track', async () => {
  const owner = createCameraSession();
  assert.equal(owner.active, false);
  const stream = fakeStream();
  assert.equal(await owner.request(async () => stream), stream);
  assert.equal(owner.active, true);
  owner.stop(); owner.stop();
  assert.equal(owner.active, false);
  assert.deepEqual(stream.tracks.map(t => t.stopped), [1, 1]);
});
test('permission arriving after cancel or unmount is immediately released', async () => {
  const owner = createCameraSession();
  let grant;
  const pending = owner.request(() => new Promise(resolve => { grant = resolve; }));
  owner.stop();
  const late = fakeStream();
  grant(late);
  assert.equal(await pending, null);
  assert.equal(owner.active, false);
  assert.deepEqual(late.tracks.map(t => t.stopped), [1, 1]);
});
test('an older permission response cannot replace a newer session', async () => {
  const owner = createCameraSession();
  let grant;
  const pending = owner.request(() => new Promise(resolve => { grant = resolve; }));
  const fresh = fakeStream();
  await owner.request(async () => fresh);
  const stale = fakeStream();
  grant(stale);
  assert.equal(await pending, null);
  assert.equal(owner.active, true);
  assert.deepEqual(stale.tracks.map(t => t.stopped), [1, 1]);
  assert.deepEqual(fresh.tracks.map(t => t.stopped), [0, 0]);
  owner.stop();
});
test('denied permission propagates for the visible fallback and never leaves an active session', async () => {
  const owner = createCameraSession();
  const denied = new Error('Permission denied'); denied.name = 'NotAllowedError';
  await assert.rejects(owner.request(async () => { throw denied; }), { name: 'NotAllowedError' });
  assert.equal(owner.active, false);
});
