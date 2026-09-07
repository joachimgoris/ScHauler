import { chromium } from 'playwright';

const browser = await chromium.launch();
const page = await browser.newPage();
page.on('pageerror', e => console.log('[pageerror]', e.message));

async function type(locator, text) {
  await locator.click();
  await locator.press('ControlOrMeta+a');
  await locator.pressSequentially(text);
  await locator.press('Tab');
  await page.waitForTimeout(150);
}

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

async function pickUp(index, adds) {  // adds: array of aria-label suffixes like '8 SCU'
  await page.locator('button.action.pickup').first().click();
  await page.waitForTimeout(400);
  for (const size of adds) {
    await page.getByLabel(`Add one ${size} box`).click();
    await page.waitForTimeout(150);
  }
  await page.getByText('CONFIRM PICK UP').click();
  await page.waitForTimeout(1200);
}

await createContract('Run A', 'Waste', 10, 'Everus Harbor', 'Baijini Point');
await createContract('Run B', 'Copper', 8, 'Everus Harbor', 'Area18');

// Pick up both at Everus Harbor
await pickUp(0, ['8 SCU', '2 SCU']);   // Run A: 8+2 = 10
await pickUp(0, ['8 SCU']);            // Run B: 8

const svg = page.locator('svg.hold-svg');
console.log('hold svg rendered:', await svg.count() === 1);
console.log('bay outlines:', await page.locator('.hold-bay').count());
console.log('boxes drawn:', await page.locator('.hold-box').count());
const labels = await page.locator('.hold-pile-label').allTextContents();
console.log('pile labels:', JSON.stringify(labels));
console.log('deliver actions:', await page.locator('button.action.deliver').count());
console.log('onboard:', (await page.locator('.cap-value').textContent()).replace(/\s+/g, ' ').trim());
await browser.close();
