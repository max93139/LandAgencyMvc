/**
 * Знімає ілюстрації для звіту ЛР5 у docs/images/lab5_*.png.
 *
 * Потрібні: запущений застосунок на свіжій базі, пакет playwright-core і Chromium.
 *   BASE       адреса застосунку (за замовчуванням http://127.0.0.1:5187)
 *   DB_PATH    шлях до landagency.db (потрібен для демонстрації ACID)
 *   CHROME_PATH шлях до виконуваного файлу Chromium
 * Запуск: NODE_PATH=<папка з node_modules> node tools/capture_lab5_figures.js
 */
const { chromium } = require('playwright-core');
const { execFileSync } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');
const zlib = require('zlib');

const BASE = process.env.BASE || 'http://127.0.0.1:5187';
const DB_PATH = process.env.DB_PATH;
const CHROME_PATH = process.env.CHROME_PATH;
const OUT = path.resolve(__dirname, '../docs/images');
const VIEWPORT = { width: 1280, height: 860 };

fs.mkdirSync(OUT, { recursive: true });

function sql(statement) {
    return execFileSync('sqlite3', [DB_PATH, statement], { encoding: 'utf8' }).trim();
}

function crc32(buffer) {
    let crc = ~0;
    for (const byte of buffer) {
        crc ^= byte;
        for (let k = 0; k < 8; k++) crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
    }
    return ~crc >>> 0;
}

function pngChunk(type, data) {
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length);
    const body = Buffer.concat([Buffer.from(type), data]);
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(crc32(body));
    return Buffer.concat([length, body, crc]);
}

function makeLandscapePng(width, height) {
    const rows = [];
    for (let y = 0; y < height; y++) {
        const row = Buffer.alloc(1 + width * 3);
        const horizon = y < height * 0.45;
        for (let x = 0; x < width; x++) {
            const t = y / height;
            const [r, g, b] = horizon
                ? [120 + 80 * t, 170 + 60 * t, 220 - 20 * t]
                : [70 + 40 * Math.sin(x / 70), 140 - 40 * t + 20 * Math.sin(x / 45), 60 + 20 * t];
            row[1 + x * 3] = r;
            row[2 + x * 3] = g;
            row[3 + x * 3] = b;
        }
        rows.push(row);
    }
    const header = Buffer.alloc(13);
    header.writeUInt32BE(width, 0);
    header.writeUInt32BE(height, 4);
    header[8] = 8;
    header[9] = 2;
    return Buffer.concat([
        Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
        pngChunk('IHDR', header),
        pngChunk('IDAT', zlib.deflateSync(Buffer.concat(rows))),
        pngChunk('IEND', Buffer.alloc(0)),
    ]);
}

async function login(page, email, password) {
    await page.goto(`${BASE}/Account/Login`);
    await page.fill('input[name=email]', email);
    await page.fill('input[name=password]', password);
    await Promise.all([page.waitForNavigation(), page.click('button[type=submit]')]);
}

async function postForm(context, tokenPage, target, fields) {
    const html = await (await context.request.get(BASE + tokenPage)).text();
    const token = /name="__RequestVerificationToken"[^>]*value="([^"]*)"/.exec(html)[1];
    return context.request.post(BASE + target, {
        form: { ...fields, __RequestVerificationToken: token },
        maxRedirects: 0,
    });
}

async function shot(page, name, options = {}) {
    await page.screenshot({ path: path.join(OUT, name), ...options });
    console.log('saved', name);
}

async function elementShot(locator, name) {
    await locator.scrollIntoViewIfNeeded();
    await locator.screenshot({ path: path.join(OUT, name) });
    console.log('saved', name);
}

(async () => {
    const browser = await chromium.launch({ executablePath: CHROME_PATH, headless: true });
    const newContext = async () => browser.newContext({ viewport: VIEWPORT, locale: 'uk-UA' });
    const photo = path.join(os.tmpdir(), 'lab5_plot_photo.png');
    fs.writeFileSync(photo, makeLandscapePng(720, 480));

    const anon = await newContext();
    let page = await anon.newPage();
    await page.goto(`${BASE}/Account/Register`);
    await shot(page, 'lab5_fig01_register.png');
    await page.goto(`${BASE}/Account/Login`);
    await shot(page, 'lab5_fig02_login.png');
    await anon.close();

    const seller = await newContext();
    page = await seller.newPage();
    await login(page, 'seller@landagency.ua', 'client123');
    await page.goto(`${BASE}/Cabinet`);
    await shot(page, 'lab5_fig03_seller_cabinet.png');
    await page.goto(`${BASE}/Client/EditOwnPlot/17`);
    await shot(page, 'lab5_fig04_seller_edit.png', { fullPage: true });
    await page.fill('input[name=Title]', 'Ділянка під забудову в с. Проліски (оновлено продавцем)');
    await Promise.all([page.waitForNavigation(), page.click('button:has-text("Зберегти зміни")')]);
    await seller.close();

    const realtor = await newContext();
    page = await realtor.newPage();
    await login(page, 'realtor@landagency.ua', 'realtor123');
    await page.goto(`${BASE}/Realtor/EditPlot/17`);
    await page.setInputFiles('input[name=mediaFile]', photo);
    await page.fill('input[name=caption]', 'Вид із південної межі ділянки');
    await Promise.all([page.waitForNavigation(), page.click('button:has-text("Додати матеріал")')]);
    await page.selectOption('select[name=kind]', 'Video');
    await page.fill('input[name=videoUrl]', 'https://example.com/plot-17-tour');
    await Promise.all([page.waitForNavigation(), page.click('button:has-text("Додати матеріал")')]);
    await elementShot(page.locator('.la-card', { hasText: 'Умови продажу та розташування на карті' }).first(), 'lab5_fig05_realtor_geo.png');
    await elementShot(page.locator('.la-card', { hasText: 'Фото та відео ділянки' }).first(), 'lab5_fig06_realtor_media.png');

    const buyer = await newContext();
    page = await buyer.newPage();
    await login(page, 'buyer@landagency.ua', 'client123');
    await page.goto(`${BASE}/Client/Details/17`);
    await shot(page, 'lab5_fig07_buyer_details.png', { fullPage: true });
    await page.goto(`${BASE}/Catalog`);
    await page.fill('input[name=desiredDistrict]', 'Київська область');
    await page.fill('input[name=minAreaHa]', '0.1');
    await page.fill('input[name=maxBudgetUsd]', '60000');
    await Promise.all([page.waitForNavigation(), page.click('button:has-text("Подати запит на автопідбір")')]);
    await page.goto(`${BASE}/Client/Details/17`);
    await page.fill('input[name=preferredDate]', '2026-12-15');
    await page.fill('input[name=timeSlot]', '10:00');
    await Promise.all([page.waitForNavigation(), page.click('button:has-text("Подати заявку на огляд")')]);
    await page.goto(`${BASE}/Cabinet`);
    for (const summary of ['Змінити параметри запиту', 'Перенести дату огляду']) {
        await page.locator('summary', { hasText: summary }).first().click();
    }
    await shot(page, 'lab5_fig08_buyer_cabinet.png', { fullPage: true });

    const conflictPlot = 18;
    await page.close();
    page = await realtor.newPage();
    await page.goto(`${BASE}/Realtor/EditPlot/${conflictPlot}`);
    sql(`update LandPlots set Title='Змінено іншим користувачем', Version='11111111-1111-1111-1111-111111111111' where Id=${conflictPlot};`);
    await page.fill('input[name=Title]', 'Моя зміна, зроблена зі старої форми');
    await Promise.all([page.waitForNavigation(), page.click('button:has-text("Зберегти зміни")')]);
    await shot(page, 'lab5_fig09_conflict.png');

    const nextDealId = Number(sql('select max(Id)+1 from SaleDeals;'));
    const year = new Date().getFullYear();
    const originalInvoice = sql('select InvoiceNumber from Payments where Id=1;');
    sql(`update Payments set InvoiceNumber='INV-${year}-${String(nextDealId).padStart(4, '0')}-1' where Id=1;`);
    const dealsBefore = sql('select count(*) from SaleDeals;');
    await postForm(realtor, '/Realtor/Deals', '/Realtor/CreateDeal', {
        landPlotId: '19', buyerId: '4', finalPriceUsd: '52000', draftContractNumber: 'ДГ-ДЕМО-1',
        notaryOffice: 'Нотаріальна контора', notaryDate: '2026-12-25T10:00',
    });
    await page.goto(`${BASE}/Realtor/Deals`);
    await shot(page, 'lab5_fig10_rollback.png');
    console.log('угод до та після невдалого створення:', dealsBefore, sql('select count(*) from SaleDeals;'));
    sql(`update Payments set InvoiceNumber='${originalInvoice}' where Id=1;`);

    await postForm(realtor, '/Realtor/Deals', '/Realtor/CreateDeal', {
        landPlotId: '19', buyerId: '4', finalPriceUsd: '52000', draftContractNumber: 'ДГ-ДЕМО-2',
        notaryOffice: 'Нотаріальна контора', notaryDate: '2026-12-25T10:00',
    });
    const dealId = sql("select Id from SaleDeals where DraftContractNumber='ДГ-ДЕМО-2';");
    const lawyer = await newContext();
    const lawyerPage = await lawyer.newPage();
    await login(lawyerPage, 'lawyer@landagency.ua', 'lawyer123');
    await postForm(lawyer, '/Lawyer/Contracts', '/Lawyer/ApproveDealContract', { dealId, lawyerNotes: 'Погоджено', decision: 'approve' });
    const payments = sql(`select Id||'|'||PaymentType from Payments where SaleDealId=${dealId};`).split('\n').map((row) => row.split('|'));
    const feeId = payments.find(([, type]) => type.startsWith('Комісійна'))[0];
    const priceId = payments.find(([, type]) => !type.startsWith('Комісійна'))[0];
    const iban = 'UA453052990000026201234567890';
    await postForm(buyer, '/Cabinet', '/Client/SubmitPayment', { paymentId: feeId, paymentReference: 'TRX-DEMO-1', senderIban: iban });
    await postForm(realtor, '/Realtor/Deals', '/Realtor/VerifyClientPayment', { paymentId: feeId, verificationNotes: 'Зараховано' });
    await postForm(buyer, '/Cabinet', '/Client/SignContractAtNotary', { dealId });
    await postForm(buyer, '/Cabinet', '/Client/SubmitPayment', { paymentId: priceId, paymentReference: 'TRX-DEMO-2', senderIban: iban });
    await postForm(realtor, '/Realtor/Deals', '/Realtor/VerifyClientPayment', { paymentId: priceId, verificationNotes: 'Зараховано' });
    await lawyerPage.goto(`${BASE}/Lawyer/Contracts`);
    await shot(lawyerPage, 'lab5_fig11_lawyer_contracts.png');

    await lawyerPage.goto(`${BASE}/Lawyer/Conclusion/17`);
    await lawyerPage.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    await shot(lawyerPage, 'lab5_fig12_lawyer_conclusion.png');

    await browser.close();
})().catch((error) => {
    console.error(error);
    process.exit(1);
});
