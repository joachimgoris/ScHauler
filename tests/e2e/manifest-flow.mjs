import { chromium } from 'playwright';

const browser = await chromium.launch();
const page = await browser.newPage();
const log = [];
page.on('pageerror', e => log.push('[pageerror] ' + e.message));

// 1. Create a contract: Waste 10 SCU, Everus Harbor -> Baijini Point
const ws = page.waitForEvent('websocket', { timeout: 15000 });
await page.goto('http://localhost:5000/contracts/new', { waitUntil: 'networkidle' });
await ws;
await page.waitForTimeout(800);
async function type(locator, text) {
  await locator.click();
  await locator.press('ControlOrMeta+a');
  await locator.pressSequentially(text);
  await locator.press('Tab');
  await page.waitForTimeout(150);
}
await type(page.getByPlaceholder('e.g. Direct Local Delivery - Hurston'), 'E2E Contract');
await type(page.getByPlaceholder('e.g. Waste'), 'Waste');
await type(page.locator('input[min="1"]'), '10');
await type(page.getByPlaceholder('Start typing...').nth(0), 'Everus Harbor');
await type(page.getByPlaceholder('Start typing...').nth(1), 'Baijini Point');
await page.waitForTimeout(300);
const saveBtn = page.getByText('Save contract');
console.log('save enabled:', await saveBtn.isEnabled());
await saveBtn.click();
await page.waitForURL('http://localhost:5000/', { timeout: 5000 });
await page.waitForTimeout(1500);

// 2. Manifest: PICK UP at Everus Harbor
const pickup = page.locator('button.action.pickup');
console.log('pickup action visible:', await pickup.count() === 1, '| text:', (await pickup.textContent()).replace(/\s+/g, ' ').trim());
await pickup.click();
await page.waitForTimeout(500);

// 3. Counters: confirm disabled until strict sum
const confirm = page.getByText('CONFIRM PICK UP');
console.log('confirm disabled at 0/10:', await confirm.isDisabled());
await page.getByLabel('Add one 8 SCU box').click();
await page.waitForTimeout(200);
console.log('confirm disabled at 8/10:', await confirm.isDisabled());
await page.getByLabel('Add one 2 SCU box').click();
await page.waitForTimeout(200);
console.log('total row:', (await page.locator('.pickup-total').textContent()).trim());
console.log('confirm enabled at 10/10:', await confirm.isEnabled());
await confirm.click();
await page.waitForTimeout(1200);

// 4. Deliver action with breakdown
const deliver = page.locator('button.action.deliver');
console.log('deliver action visible:', await deliver.count() === 1, '| text:', (await deliver.textContent()).replace(/\s+/g, ' ').trim());
console.log('onboard tally:', (await page.locator('.cap-value').textContent()).replace(/\s+/g, ' ').trim());
await deliver.click();
await page.waitForTimeout(1200);
console.log('empty state after deliver:', await page.getByText('No active cargo').count() === 1);

console.log(log.length ? log.join('\n') : '(no page errors)');
await browser.close();
