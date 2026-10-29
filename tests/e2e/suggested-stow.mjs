import { chromium } from 'playwright';
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1500, height: 1600 } });
page.on('pageerror', e => console.log('[pageerror]', e.message));
async function type(l, t) { await l.click(); await l.press('ControlOrMeta+a'); await l.pressSequentially(t); await l.press('Tab'); await page.waitForTimeout(150); }
async function createContract(name, commodity, scu, from, to) {
  const ws = page.waitForEvent('websocket', { timeout: 15000 }).catch(() => {});
  await page.goto('http://localhost:5000/contracts/new', { waitUntil: 'networkidle' });
  await ws; await page.waitForTimeout(800);
  await type(page.getByPlaceholder('e.g. Direct Local Delivery - Hurston'), name);
  await type(page.getByPlaceholder('e.g. Waste'), commodity);
  await type(page.locator('input[min="1"]'), String(scu));
  await type(page.getByPlaceholder('Start typing...').nth(0), from);
  await type(page.getByPlaceholder('Start typing...').nth(1), to);
  await page.getByText('Save contract').click();
  await page.waitForURL('http://localhost:5000/', { timeout: 5000 });
  await page.waitForTimeout(1200);
}
async function pickUp(adds) {
  await page.locator('button.action.pickup').first().click();
  await page.waitForTimeout(400);
  for (const s of adds) { await page.getByLabel(`Add one ${s} box`).click(); await page.waitForTimeout(120); }
  await page.getByText('CONFIRM PICK UP').click();
  await page.waitForTimeout(1200);
}
await createContract('Run A', 'Waste', 42, 'Everus Harbor', 'Baijini Point');
await createContract('Run B', 'Copper', 24, 'Everus Harbor', 'Area18');

// Before any pickup: both destinations are route cards (pending-only), ghosts cover 66 cells.
console.log('cards before pickup:', await page.locator('.route-card').count());
console.log('ghost cells:', await page.locator('.hold-ghost').count(), '(expect 66)');
console.log('legend:', JSON.stringify((await page.locator('.cap-chip').allTextContents()).map(s => s.replace(/\s+/g, ' ').trim())));

await pickUp(['32 SCU', '8 SCU', '2 SCU']);
console.log('ghost cells after first pickup:', await page.locator('.hold-ghost').count(), '(expect 24)');
console.log('stop badges on stacks:', JSON.stringify(await page.locator('.hold-box-stop').allTextContents()));
console.log('notes:', JSON.stringify((await page.locator('.hold-note').allTextContents()).map(s => s.trim())));

await pickUp(['24 SCU']);
console.log('ghost cells after second pickup:', await page.locator('.hold-ghost').count(), '(expect 0)');
console.log('stow conflicts:', await page.locator('.hold-note-warn').count(), '(expect 0: second pickup went to its ghost region)');

// Swap drop order: the deeper cargo now drops first -> stow check note appears.
await page.locator('.route-card').nth(1).locator('.route-step').first().click();
await page.waitForTimeout(800);
console.log('dests after ▲:', JSON.stringify(await page.locator('.route-dest').allTextContents()));
console.log('stow conflicts after swap:', JSON.stringify((await page.locator('.hold-note-warn').allTextContents()).map(s => s.trim())));
await page.screenshot({ path: '/tmp/suggested-stow.png', fullPage: false });
await browser.close();
