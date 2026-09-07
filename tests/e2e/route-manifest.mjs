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
await pickUp(['32 SCU', '8 SCU', '2 SCU']);
await pickUp(['24 SCU']);

console.log('cards:', await page.locator('.route-card').count());
console.log('badges:', JSON.stringify(await page.locator('.route-stop-badge').allTextContents()));
console.log('dests:', JSON.stringify(await page.locator('.route-dest').allTextContents()));
console.log('stowlines:', JSON.stringify((await page.locator('.route-stowline').allTextContents()).map(s => s.trim())));
// reorder: move second card up
await page.locator('.route-card').nth(1).locator('.route-step').first().click();
await page.waitForTimeout(400);
console.log('dests after ▲:', JSON.stringify(await page.locator('.route-dest').allTextContents()));
// isolate via card
await page.locator('.route-card').first().click();
await page.waitForTimeout(400);
console.log('isolate status:', (await page.locator('.hold-isolate-status').textContent()).trim());
console.log('dimmed hold stacks:', await page.locator('.hold-box-dimmed').count(), '| dimmed cards:', await page.locator('.route-card-dimmed').count());
await page.getByRole('button', { name: 'SHOW ALL' }).click();
await page.waitForTimeout(300);
// deliver via card row
console.log('deliver panels in stop list:', await page.locator('button.action.deliver').count());
await page.locator('.route-row-action').first().click();
await page.waitForTimeout(1200);
console.log('cards after deliver:', await page.locator('.route-card').count());
console.log('onboard after deliver:', (await page.locator('.cap-value').textContent()).replace(/\s+/g, ' ').trim());
await page.waitForTimeout(300);
await page.evaluate(() => window.scrollTo(0, 0));
await page.waitForTimeout(300);
await page.screenshot({ path: '/tmp/route-v1.png', fullPage: false });
await browser.close();
