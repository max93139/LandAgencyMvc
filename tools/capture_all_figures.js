const { chromium } = require('/Users/mac/Desktop/projects/automated-estate-valuation/node_modules/playwright');
const fs = require('fs');
const path = require('path');

const outDir = path.resolve(__dirname, '../docs/images');
if (!fs.existsSync(outDir)) {
    fs.mkdirSync(outDir, { recursive: true });
}

async function renderErDiagram(browser) {
    const page = await browser.newPage({ viewport: { width: 1300, height: 860 } });
    const erHtml = `
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset="utf-8">
        <style>
            body {
                margin: 0;
                padding: 24px;
                background: #0f172a;
                font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
                color: #f8fafc;
            }
            .title {
                text-align: center;
                margin-bottom: 20px;
            }
            .title h1 {
                margin: 0 0 6px 0;
                font-size: 22px;
                color: #38bdf8;
                font-weight: 700;
            }
            .title p {
                margin: 0;
                font-size: 13px;
                color: #94a3b8;
            }
            .grid {
                display: grid;
                grid-template-columns: repeat(4, 1fr);
                gap: 16px;
            }
            .table-card {
                background: #1e293b;
                border: 1px solid #334155;
                border-radius: 8px;
                overflow: hidden;
                box-shadow: 0 4px 12px rgba(0,0,0,0.3);
            }
            .table-header {
                background: #0284c7;
                color: #ffffff;
                padding: 8px 12px;
                font-weight: 700;
                font-size: 14px;
                display: flex;
                justify-content: space-between;
                align-items: center;
            }
            .table-header.finance { background: #059669; }
            .table-header.legal { background: #7c3aed; }
            .table-header.plots { background: #d97706; }
            .badge {
                font-size: 10px;
                padding: 2px 6px;
                background: rgba(255,255,255,0.2);
                border-radius: 4px;
            }
            .fields {
                padding: 10px 12px;
                font-size: 11px;
                line-height: 1.6;
            }
            .field-row {
                display: flex;
                justify-content: space-between;
                border-bottom: 1px solid #334155;
                padding: 3px 0;
            }
            .field-row:last-child {
                border-bottom: none;
            }
            .name {
                font-family: "SF Mono", Menlo, Consolas, monospace;
                color: #e2e8f0;
            }
            .pk { color: #facc15; font-weight: bold; }
            .fk { color: #38bdf8; font-weight: bold; }
            .type { color: #94a3b8; font-family: "SF Mono", monospace; }
            .rel-legend {
                margin-top: 18px;
                padding: 12px;
                background: #1e293b;
                border: 1px solid #334155;
                border-radius: 8px;
                display: flex;
                justify-content: space-around;
                font-size: 12px;
                color: #cbd5e1;
            }
            .rel-item strong { color: #38bdf8; }
        </style>
    </head>
    <body>
        <div class="title">
            <h1>Концептуальна схема зв'язків реляційної бази даних SQLite (8 таблиць)</h1>
            <p>LandAgencyMvc — Автоматизація земельного агентства | Entity Framework Core 10 | DeleteBehavior.Restrict</p>
        </div>
        <div class="grid">
            <div class="table-card">
                <div class="table-header"><span>Users</span><span class="badge">14 rows</span></div>
                <div class="fields">
                    <div class="field-row"><span class="name pk">PK Id</span><span class="type">int</span></div>
                    <div class="field-row"><span class="name">FullName</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">Email [UK]</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">Phone</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">Role</span><span class="type">int</span></div>
                    <div class="field-row"><span class="name">Password</span><span class="type">text(PBKDF2)</span></div>
                    <div class="field-row"><span class="name">BankDetails</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">IsActive</span><span class="type">bool</span></div>
                </div>
            </div>

            <div class="table-card">
                <div class="table-header plots"><span>LandPlots</span><span class="badge">16 rows</span></div>
                <div class="fields">
                    <div class="field-row"><span class="name pk">PK Id</span><span class="type">int</span></div>
                    <div class="field-row"><span class="name">CadastralNumber [UK]</span><span class="type">text(19)</span></div>
                    <div class="field-row"><span class="name">Title</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">Location</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">AreaHectares</span><span class="type">real</span></div>
                    <div class="field-row"><span class="name">Purpose</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">PriceUsd</span><span class="type">decimal</span></div>
                    <div class="field-row"><span class="name">Status</span><span class="type">int</span></div>
                    <div class="field-row"><span class="name fk">FK SellerId</span><span class="type">-> Users</span></div>
                    <div class="field-row"><span class="name fk">FK AssignedRealtorId</span><span class="type">-> Users</span></div>
                </div>
            </div>

            <div class="table-card">
                <div class="table-header legal"><span>PlotDocuments</span><span class="badge">16 rows</span></div>
                <div class="fields">
                    <div class="field-row"><span class="name pk">PK Id</span><span class="type">int</span></div>
                    <div class="field-row"><span class="name fk">FK LandPlotId</span><span class="type">-> LandPlots</span></div>
                    <div class="field-row"><span class="name">Title</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">DocType</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">DocumentNumber</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">FileHash (SHA-256)</span><span class="type">text(64)</span></div>
                    <div class="field-row"><span class="name">IsVerified</span><span class="type">bool</span></div>
                    <div class="field-row"><span class="name">IssueDate</span><span class="type">datetime</span></div>
                </div>
            </div>

            <div class="table-card">
                <div class="table-header legal"><span>LegalConclusions</span><span class="badge">16 rows</span></div>
                <div class="fields">
                    <div class="field-row"><span class="name pk">PK Id</span><span class="type">int</span></div>
                    <div class="field-row"><span class="name fk">FK LandPlotId [1:1]</span><span class="type">-> LandPlots</span></div>
                    <div class="field-row"><span class="name fk">FK LawyerId</span><span class="type">-> Users</span></div>
                    <div class="field-row"><span class="name">ConclusionNumber [UK]</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">RiskLevel</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">IsApproved</span><span class="type">bool</span></div>
                    <div class="field-row"><span class="name">DzkExtractNumber</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">CreatedAt</span><span class="type">datetime</span></div>
                </div>
            </div>

            <div class="table-card">
                <div class="table-header"><span>ViewingRequests</span><span class="badge">16 rows</span></div>
                <div class="fields">
                    <div class="field-row"><span class="name pk">PK Id</span><span class="type">int</span></div>
                    <div class="field-row"><span class="name fk">FK LandPlotId</span><span class="type">-> LandPlots</span></div>
                    <div class="field-row"><span class="name fk">FK BuyerId</span><span class="type">-> Users</span></div>
                    <div class="field-row"><span class="name fk">FK RealtorId</span><span class="type">-> Users</span></div>
                    <div class="field-row"><span class="name">PreferredDate</span><span class="type">datetime</span></div>
                    <div class="field-row"><span class="name">TimeSlot</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">Status</span><span class="type">int</span></div>
                </div>
            </div>

            <div class="table-card">
                <div class="table-header"><span>SelectionRequests</span><span class="badge">10 rows</span></div>
                <div class="fields">
                    <div class="field-row"><span class="name pk">PK Id</span><span class="type">int</span></div>
                    <div class="field-row"><span class="name fk">FK BuyerId</span><span class="type">-> Users</span></div>
                    <div class="field-row"><span class="name">DesiredDistrict</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">MinAreaHa</span><span class="type">real</span></div>
                    <div class="field-row"><span class="name">MaxBudgetUsd</span><span class="type">decimal</span></div>
                    <div class="field-row"><span class="name">Purpose</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">MatchedPlotsCount</span><span class="type">int</span></div>
                </div>
            </div>

            <div class="table-card">
                <div class="table-header finance"><span>SaleDeals</span><span class="badge">14 rows</span></div>
                <div class="fields">
                    <div class="field-row"><span class="name pk">PK Id</span><span class="type">int</span></div>
                    <div class="field-row"><span class="name fk">FK LandPlotId</span><span class="type">-> LandPlots</span></div>
                    <div class="field-row"><span class="name fk">FK BuyerId</span><span class="type">-> Users</span></div>
                    <div class="field-row"><span class="name fk">FK RealtorId</span><span class="type">-> Users</span></div>
                    <div class="field-row"><span class="name fk">FK LawyerId</span><span class="type">-> Users</span></div>
                    <div class="field-row"><span class="name">DraftContractNumber [UK]</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">FinalPriceUsd</span><span class="type">decimal</span></div>
                    <div class="field-row"><span class="name">AgencyCommissionUsd</span><span class="type">decimal</span></div>
                    <div class="field-row"><span class="name">Status</span><span class="type">int</span></div>
                </div>
            </div>

            <div class="table-card">
                <div class="table-header finance"><span>Payments</span><span class="badge">28 rows</span></div>
                <div class="fields">
                    <div class="field-row"><span class="name pk">PK Id</span><span class="type">int</span></div>
                    <div class="field-row"><span class="name fk">FK SaleDealId</span><span class="type">-> SaleDeals</span></div>
                    <div class="field-row"><span class="name">InvoiceNumber [UK]</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">AmountUsd</span><span class="type">decimal</span></div>
                    <div class="field-row"><span class="name">PaymentType</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">RecipientIban</span><span class="type">text</span></div>
                    <div class="field-row"><span class="name">IsPaid</span><span class="type">bool</span></div>
                    <div class="field-row"><span class="name">Status</span><span class="type">int</span></div>
                </div>
            </div>
        </div>

        <div class="rel-legend">
            <div class="rel-item"><strong>1 : N</strong> Users → LandPlots (SellerId) [Restrict]</div>
            <div class="rel-item"><strong>1 : N</strong> LandPlots → PlotDocuments [Restrict]</div>
            <div class="rel-item"><strong>1 : 1</strong> LandPlots ↔ LegalConclusions [Restrict]</div>
            <div class="rel-item"><strong>1 : N</strong> LandPlots → SaleDeals [Restrict]</div>
            <div class="rel-item"><strong>1 : N</strong> SaleDeals → Payments [Restrict]</div>
        </div>
    </body>
    </html>
    `;

    await page.setContent(erHtml);
    await page.waitForTimeout(600);
    const erPath1 = path.join(outDir, 'er_diagram.png');
    const erPath2 = path.join(outDir, 'fig01_er_diagram.png');
    await page.screenshot({ path: erPath1, fullPage: true });
    await page.screenshot({ path: erPath2, fullPage: true });
    console.log('Saved ER diagram to', erPath1, 'and', erPath2);
    await page.close();
}

async function captureViews(browser) {
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const page = await context.newPage();

    console.log('Logging in as Realtor/Admin...');
    await page.goto('http://127.0.0.1:5099/Account/Login');
    await page.fill('input[name="email"]', 'admin@landagency.ua');
    await page.fill('input[name="password"]', 'admin123');
    await page.click('button[type="submit"]');
    await page.waitForTimeout(800);

    // Fig 2: Realtor Analytics Dashboard
    console.log('Capturing Fig 2: Analytics Dashboard...');
    await page.goto('http://127.0.0.1:5099/Realtor/Analytics');
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(outDir, 'fig02_dashboard.png'), fullPage: false });

    // Fig 3: Client Catalog Filter
    console.log('Capturing Fig 3: Catalog Filter...');
    await page.goto('http://127.0.0.1:5099/Client/Catalog?purpose=01.03&maxBudget=60000');
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(outDir, 'fig03_filter_plots.png'), fullPage: false });

    // Fig 4: Client Catalog Sorted by Price
    console.log('Capturing Fig 4: Catalog Price Sort...');
    await page.goto('http://127.0.0.1:5099/Client/Catalog?sort=Price&desc=false');
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(outDir, 'fig04_filter_sort_sql.png'), fullPage: false });

    // Fig 5: Period Turnover Month
    console.log('Capturing Fig 5: Turnover Month...');
    await page.goto('http://127.0.0.1:5099/Realtor/Analytics?kind=Month&date=2026-10-15&sort=Turnover&desc=true');
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(outDir, 'fig05_period_turnover_month.png'), fullPage: false });

    // Fig 6: Custom Period Turnover
    console.log('Capturing Fig 6: Custom Turnover...');
    await page.goto('http://127.0.0.1:5099/Realtor/Analytics?kind=Custom&from=2025-07-01&to=2026-11-01&sort=Turnover&desc=true');
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(outDir, 'fig06_period_turnover_custom.png'), fullPage: false });

    // Fig 7: Agent Performance
    console.log('Capturing Fig 7: Agent Performance...');
    await page.evaluate(() => window.scrollBy(0, 480));
    await page.screenshot({ path: path.join(outDir, 'fig07_agent_performance.png'), fullPage: false });

    // Fig 8: Land Bank Audit (Log in as Lawyer)
    console.log('Capturing Fig 8: Land Bank Audit as Lawyer...');
    await page.goto('http://127.0.0.1:5099/Account/Login');
    await page.fill('input[name="email"]', 'lawyer@landagency.ua');
    await page.fill('input[name="password"]', 'lawyer123');
    await page.click('button[type="submit"]');
    await page.waitForTimeout(600);
    await page.goto('http://127.0.0.1:5099/Lawyer/LandBank?RiskLevel=%D0%9D%D0%B8%D0%B7%D1%8C%D0%BA%D0%B8%D0%B9&OnlyVerifiedDocs=true');
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(outDir, 'fig08_land_bank_audit.png'), fullPage: false });

    // Switch back to Realtor/Admin for CRM / Clients
    console.log('Switching back to Realtor/Admin for CRM...');
    await page.goto('http://127.0.0.1:5099/Account/Login');
    await page.fill('input[name="email"]', 'admin@landagency.ua');
    await page.fill('input[name="password"]', 'admin123');
    await page.click('button[type="submit"]');
    await page.waitForTimeout(600);

    // Fig 9: User search by email
    console.log('Capturing Fig 9: User Search in CRM...');
    await page.goto('http://127.0.0.1:5099/Realtor/Clients?searchEmail=seller%40landagency.ua');
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(outDir, 'fig09_user_search.png'), fullPage: false });

    // Fig 10: Edit Client Modal (with password change field)
    console.log('Capturing Fig 10: Edit Client Modal in CRM...');
    await page.goto('http://127.0.0.1:5099/Realtor/Clients');
    await page.waitForTimeout(400);
    await page.locator('button:has-text("Редагувати")').first().click();
    await page.waitForTimeout(500);
    await page.screenshot({ path: path.join(outDir, 'fig10_password_verify.png'), fullPage: false });

    // Fig 11: Add user form
    console.log('Capturing Fig 11: Add user form in CRM...');
    await page.goto('http://127.0.0.1:5099/Realtor/Clients');
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(outDir, 'fig11_add_user_transaction.png'), fullPage: false });

    // Fig 12: User Management & Restrict
    console.log('Capturing Fig 12: User Management...');
    await page.evaluate(() => window.scrollBy(0, 420));
    await page.screenshot({ path: path.join(outDir, 'fig12_user_management_restrict.png'), fullPage: false });

    // Fig 13: Plot Details
    console.log('Capturing Fig 13: Plot Details...');
    await page.goto('http://127.0.0.1:5099/Client/Details/1');
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(outDir, 'fig13_linq_showcase_set_ops.png'), fullPage: false });

    // Fig 14: Realtor Analytics summary view
    console.log('Capturing Fig 14: Analytics Summary...');
    await page.goto('http://127.0.0.1:5099/Realtor/Analytics?kind=HalfYear&date=2026-05-01');
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(outDir, 'fig14_linq_showcase_advanced_plinq.png'), fullPage: false });

    console.log('All 14 figures captured successfully!');
    await context.close();
}

(async () => {
    const browser = await chromium.launch({ channel: 'chrome', headless: true });
    try {
        await renderErDiagram(browser);
        await captureViews(browser);
    } finally {
        await browser.close();
    }
})();
