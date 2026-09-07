import { chromium } from 'playwright';
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1180, height: 1400 } });
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
await pickUp(['32 SCU', '8 SCU', '2 SCU']);  // 42
await pickUp(['24 SCU']);                    // 24

console.log('bay chips:', JSON.stringify(await page.locator('.hold-baychip').allTextContents()));
console.log('stacks drawn:', await page.locator('button.hold-box').count());
console.log('ruler ticks:', await page.locator('.hold-tick').count());
console.log('status:', (await page.locator('.hold-isolate-status').textContent()).trim());

// isolate: tap a Baijini box, count dimmed
await page.locator('button.hold-box').first().click();
await page.waitForTimeout(400);
console.log('after tap status:', (await page.locator('.hold-isolate-status').textContent()).trim());
console.log('dimmed stacks:', await page.locator('.hold-box-dimmed').count());
await page.getByRole('button', { name: 'SHOW ALL' }).click();
await page.waitForTimeout(300);
console.log('dimmed after show all:', await page.locator('.hold-box-dimmed').count());

await page.evaluate(() => window.scrollTo(0, 0));
await page.waitForTimeout(300);
const bb = await page.locator('section.hold').boundingBox();
await page.screenshot({ path: '/tmp/hold-v2.png', clip: bb });
await browser.close();
