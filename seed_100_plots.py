import sqlite3
import hashlib
import random
from datetime import datetime, timedelta

DB_PATH = "LandAgencyMvc/landagency.db"

DISTRICTS = [
    {
        "raion": "Обухівський р-н",
        "koatuu": "32231",
        "settlements": [
            ("смт Козин", ["вул. Старокиївська", "вул. Солов'яненка", "вул. Лугова", "вул. Берегова", "пров. Дніпровий"]),
            ("с. Підгірці", ["вул. Озерна", "вул. Васильківська", "вул. Богдана Хмельницького", "вул. Польова"]),
            ("с. Романків", ["вул. Лісова", "вул. Соснова", "вул. Зарічна", "вул. Козацька"]),
            ("м. Українка", ["вул. Дніпровська", "вул. Південна", "вул. Вишнева", "вул. Студентська"]),
            ("с. Безрадичі", ["вул. Центральна", "вул. Київська", "вул. Шевченка"]),
            ("с. Трипілля", ["вул. Героїв Трипілля", "вул. Шевченка", "вул. Набережна"])
        ],
        "price_factor": 1.45
    },
    {
        "raion": "Бучанський р-н",
        "koatuu": "32224",
        "settlements": [
            ("м. Буча", ["вул. Яснополянська", "вул. Тарасівська", "вул. Інститутська", "вул. Лісова", "вул. Шевченка"]),
            ("м. Ірпінь", ["вул. Університетська", "вул. Соборна", "вул. Пушкінська", "вул. Чехова", "вул. 11-а Лінія"]),
            ("с. Дмитрівка", ["вул. Молодіжна", "вул. Садова", "вул. Лісова", "вул. Київська"]),
            ("с. Гореничі", ["вул. Лесі Українки", "вул. Соснова", "вул. Польова", "вул. Житомирська"]),
            ("с. Петропавлівська Борщагівка", ["вул. Соборна", "вул. Весняна", "вул. Оксамитова", "вул. Ярослава Мудрого"]),
            ("с. Софіївська Борщагівка", ["вул. Соборна", "вул. Скіфська", "вул. Шалімова", "вул. Пушкіна"]),
            ("смт Ворзель", ["вул. Курортна", "вул. Тюльпанова", "вул. Кленова", "вул. Миру"]),
            ("смт Гостомель", ["вул. Свято-Покровська", "вул. Остромирська", "вул. Лугова", "вул. Паркова"])
        ],
        "price_factor": 1.25
    },
    {
        "raion": "Бориспільський р-н",
        "koatuu": "32212",
        "settlements": [
            ("с. Гора", ["вул. Центральна", "вул. Бориспільська", "вул. Лісова", "вул. Сонячна"]),
            ("с. Щасливе", ["вул. Демидівська", "вул. Лисенка", "вул. Героїв Майдану"]),
            ("с. Гнідин", ["вул. Заводська", "вул. Молодіжна", "урочище Кальне", "вул. Набережна"]),
            ("с. Вишеньки", ["вул. Центральна", "вул. Набережна", "вул. Садова", "вул. Лугова"]),
            ("с. Чубинське", ["вул. Виставкова", "вул. Погребняка", "вул. Яблунева"]),
            ("с. Проліски", ["вул. Броварська", "вул. Паркова", "вул. Кооперативна"])
        ],
        "price_factor": 1.15
    },
    {
        "raion": "Вишгородський р-н",
        "koatuu": "32218",
        "settlements": [
            ("м. Вишгород", ["вул. Шолуденка", "вул. Спаська", "вул. Київська", "вул. Дніпровська"]),
            ("с. Нові Петрівці", ["вул. Свято-Покровська", "вул. Межигірська", "вул. Ватутіна", "вул. Лісова"]),
            ("с. Старі Петрівці", ["вул. Князя Святослава", "вул. Польова", "вул. Садова"]),
            ("с. Лютіж", ["вул. Вітряного", "вул. Набережна", "вул. Лісова"]),
            ("с. Хотянівка", ["масив Дніпровський", "вул. Садова", "вул. Деснянська", "вул. Лісова"]),
            ("с. Осещина", ["вул. Київська", "вул. Лугова", "урочище Десна"])
        ],
        "price_factor": 1.10
    },
    {
        "raion": "Броварський р-н",
        "koatuu": "32214",
        "settlements": [
            ("с. Погреби", ["вул. Придеснянська", "вул. Соснова", "вул. Центральна", "вул. Грушевського"]),
            ("с. Зазим'я", ["вул. Деснянська", "вул. Широка", "вул. Київська", "вул. Лісова"]),
            ("с. Пухівка", ["вул. Набережна", "вул. Центральна", "вул. Леніна"]),
            ("с. Княжичі", ["вул. Київська", "вул. Слави", "вул. Соборна", "вул. Миру"]),
            ("с. Требухів", ["вул. Броварська", "вул. Польова", "вул. Шевченка"]),
            ("с. Рожівка", ["вул. Лісова", "вул. Садова", "вул. Польова"])
        ],
        "price_factor": 1.05
    },
    {
        "raion": "Фастівський р-н",
        "koatuu": "32249",
        "settlements": [
            ("с. Віта-Поштова", ["вул. Ярослава Мудрого", "вул. Боярська", "вул. Звенигородська", "вул. Садова"]),
            ("с. Юрівка", ["вул. Шкільна", "вул. Шевченка", "вул. Лугова"]),
            ("с. Тарасівка", ["вул. Київська", "вул. Шевченка", "вул. Залізнична"]),
            ("смт Глеваха", ["вул. Вокзальна", "вул. Польова", "вул. Київська"]),
            ("с. Мархалівка", ["вул. Лісова", "вул. Антонова", "вул. Миру"]),
            ("с. Іванковичі", ["вул. Соборна", "вул. Польова", "вул. Спортивна"])
        ],
        "price_factor": 1.00
    },
    {
        "raion": "Білоцерківський р-н",
        "koatuu": "32204",
        "settlements": [
            ("м. Біла Церква", ["вул. Леваневського", "вул. Таращанська", "вул. Сквирське шосе"]),
            ("с. Фурси", ["вул. Центральна", "вул. Ярослава Мудрого", "вул. Польова"]),
            ("смт Терезине", ["вул. Першотравнева", "вул. Молодіжна"]),
            ("с. Шкарівка", ["вул. Набережна", "вул. Миру"])
        ],
        "price_factor": 0.75
    }
]

PURPOSE_TEMPLATES = [
    {
        "code": "02.01",
        "name": "02.01 Для будівництва і обслуговування житлового будинку, господарських будівель і споруд (присадибна ділянка)",
        "titles": [
            "Ділянка під котеджне будівництво у затишному масиві",
            "Земельна ділянка під заміський будинок біля лісу",
            "Кутова ділянка для будівництва котеджу з усіма комунікаціями",
            "Присадибна ділянка правильної форми серед сучасної забудови",
            "Ділянка під індивідуальне житлове будівництво з виходом до водойми",
            "Затишна ділянка під будівництво будинку з дорослими соснами",
            "Перспективна ділянка у новому котеджному кварталі",
            "Ділянка під приватну садибу з асфальтованим під'їздом"
        ],
        "area_range": (0.08, 0.45),
        "base_price_per_sotka": (1800, 5500),
        "weight": 55
    },
    {
        "code": "01.03",
        "name": "01.03 Для ведення особистого селянського господарства",
        "titles": [
            "Земельна ділянка для ведення ОСГ з перспективою зміни цільового",
            "Рівна ділянка під ОСГ біля асфальтованої траси",
            "Масив ОСГ з можливістю підключення електроенергії",
            "Ділянка під фермерство та особисте селянське господарство",
            "Земельна ділянка під ОСГ поруч із житловим сектором"
        ],
        "area_range": (0.25, 2.50),
        "base_price_per_sotka": (450, 1500),
        "weight": 18
    },
    {
        "code": "03.07",
        "name": "03.07 Для будівництва та обслуговування будівель торгівлі",
        "titles": [
            "Фасадна ділянка під торговий центр або супермаркет",
            "Комерційна ділянка першої лінії вздовж жвавої магістралі",
            "Земельна ділянка під автосервіс, шиномонтаж або СТО",
            "Фасадна комерційна ділянка під ресторанно-готельний комплекс",
            "Ділянка під торгово-офісний центр або логістику"
        ],
        "area_range": (0.20, 1.80),
        "base_price_per_sotka": (3500, 9500),
        "weight": 12
    },
    {
        "code": "01.05",
        "name": "01.05 Для індивідуального садівництва",
        "titles": [
            "Мальовнича садова ділянка в обжитому садовому товаристві",
            "Ділянка під дачне будівництво та садівництво біля озера",
            "Садова ділянка з садом та електрикою на межі",
            "Ділянка в СТ біля річки, ідеально під зону відпочинку"
        ],
        "area_range": (0.06, 0.20),
        "base_price_per_sotka": (1000, 2800),
        "weight": 10
    },
    {
        "code": "01.01",
        "name": "01.01 Для ведення товарного сільськогосподарського виробництва",
        "titles": [
            "Земельний пай для товарного агровиробництва з чорноземом",
            "Агромасив для ведення товарного сільгоспвиробництва",
            "Родюча земельна ділянка для товарного агробізнесу"
        ],
        "area_range": (1.20, 4.80),
        "base_price_per_sotka": (250, 650),
        "weight": 5
    }
]

DESCRIPTIONS_DETAILS = [
    "Ділянка правильної прямокутної форми, рівний сухий рельєф без перепадів висот, не підтоплюється у паводковий період.",
    "Електрика 15 кВт підведена до межі ділянки (встановлено двозонний лічильник та захисний автомат). Вздовж вулиці проходить магістральний газ.",
    "Зручний під'їзд: тверде покриття з якісного асфальту, взимку дороги систематично розчищаються комунальними службами.",
    "Сусіди активно забудовуються сучасними котеджами, вулиця освітлена, поруч зупинка транспорту, продуктовий маркет та дитячий майданчик.",
    "Документи повністю готові до швидкої нотаріальної угоди: право власності зареєстровано в ДРРП, кадастровий номер присвоєно, межі винесені в натуру геодезистами."
]

IBAN_BANKS = ["305299", "322001", "300465", "380805", "325365"]
SELLER_IDS = [3, 4, 5]
LAWYER_ID = 2
REALTOR_ID = 1

def generate_cadastral(koatuu, zone, block, parcel):
    return f"{koatuu}{zone:05d}:{block:02d}:{parcel:03d}:{parcel + 1000:04d}"

def generate_iban():
    bank = random.choice(IBAN_BANKS)
    acc = "".join([str(random.randint(0, 9)) for _ in range(19)])
    return f"UA82{bank}{acc}"

def main():
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()

    cur.execute("SELECT MAX(Id) FROM LandPlots;")
    last_id = cur.fetchone()[0] or 0
    print(f"Current highest LandPlot ID: {last_id}")

    used_cadastrals = set()
    cur.execute("SELECT CadastralNumber FROM LandPlots;")
    for row in cur.fetchall():
        used_cadastrals.add(row[0])

    weighted_purposes = []
    for p in PURPOSE_TEMPLATES:
        weighted_purposes.extend([p] * p["weight"])

    plots_to_insert = []
    conclusions_to_insert = []
    docs_to_insert = []

    start_date = datetime(2026, 8, 1, 10, 0, 0)

    for i in range(1, 101):
        target_id = last_id + i
        dist = random.choice(DISTRICTS)
        settlement, streets = random.choice(dist["settlements"])
        street = random.choice(streets)
        house_num = random.randint(1, 120)

        location_str = f"Київська обл., {dist['raion']}, {settlement}, {street}, {house_num}"

        pt = random.choice(weighted_purposes)
        title_base = random.choice(pt["titles"])
        title = f"{title_base} ({settlement.replace('смт ', '').replace('с. ', '').replace('м. ', '')})"

        area_ha = round(random.uniform(pt["area_range"][0], pt["area_range"][1]), 2)
        sotkas = area_ha * 100

        base_sotka_price = random.uniform(pt["base_price_per_sotka"][0], pt["base_price_per_sotka"][1])
        total_usd = round(sotkas * base_sotka_price * dist["price_factor"], -2)
        if total_usd < 5000:
            total_usd = 5000.0

        while True:
            zone = random.randint(100, 899)
            block = random.randint(1, 25)
            parcel = random.randint(10, 990)
            cad = generate_cadastral(dist["koatuu"], zone, block, parcel)
            if cad not in used_cadastrals:
                used_cadastrals.add(cad)
                break

        desc_items = random.sample(DESCRIPTIONS_DETAILS, k=4)
        full_desc = f"Продається перспективна земельна ділянка за адресою: {location_str}.\n" + " ".join(desc_items)

        doc_num = f"{random.randint(210000000, 399999999)}"
        doc_date = f"{random.randint(10, 28)}.{random.randint(1, 12):02d}.202{random.randint(2, 5)}"
        ownership_doc = f"Витяг з ДРРП про реєстрацію права власності № {doc_num} від {doc_date}"

        iban = generate_iban()
        seller_id = random.choice(SELLER_IDS)
        status = 1

        plot_created_dt = start_date + timedelta(days=random.randint(1, 75), hours=random.randint(0, 23), minutes=random.randint(0, 59))
        created_at_str = plot_created_dt.strftime("%Y-%m-%d %H:%M:%S.%f")

        plots_to_insert.append((
            target_id,
            cad,
            title,
            location_str,
            area_ha,
            pt["name"],
            f"{total_usd:.1f}",
            full_desc,
            ownership_doc,
            iban,
            status,
            seller_id,
            REALTOR_ID,
            created_at_str
        ))

        concl_num = f"{2026:04d}-{target_id:04d}"
        dzk_ext_num = f"НВ-{dist['koatuu']}{random.randint(10000, 99999)}/2026"
        concl_text = (
            f"Правовий аудит ділянки (кадастровий номер {cad}) проведено успішно. "
            f"За даними ДЗК та ДРРП обтяження, судові арешти, іпотеки чи сервітути відсутні. "
            f"Межі ділянки відповідають кадастровому плану. "
            f"Суб'єкт права власності підтверджений. Ризики угоди оцінено як мінімальні."
        )

        conclusions_to_insert.append((
            concl_num,
            target_id,
            LAWYER_ID,
            1,
            1,
            1,
            1,
            1,
            "Низький (Чисто)",
            dzk_ext_num,
            concl_text,
            1,
            created_at_str
        ))

        hash_dzk = hashlib.sha256(f"dzk_{cad}_{target_id}".encode()).hexdigest()
        hash_drrp = hashlib.sha256(f"drrp_{cad}_{target_id}".encode()).hexdigest()

        docs_to_insert.append((
            target_id,
            "Витяг з Державного земельного кадастру (ДЗК)",
            "ДЗК",
            dzk_ext_num,
            plot_created_dt.strftime("%Y-%m-%d"),
            "/uploads/documents/dzk_sample.pdf",
            1,
            hash_dzk,
            1
        ))

        docs_to_insert.append((
            target_id,
            "Витяг з Державного реєстру речових прав на нерухоме майно (ДРРП)",
            "ДРРП",
            doc_num,
            plot_created_dt.strftime("%Y-%m-%d"),
            "/uploads/documents/drrp_sample.pdf",
            1,
            hash_drrp,
            1
        ))

    cur.executemany("""
        INSERT INTO LandPlots (
            Id, CadastralNumber, Title, Location, AreaHectares, Purpose, PriceUsd,
            Description, OwnershipDocNumber, SellerBankIban, Status, SellerId,
            AssignedRealtorId, CreatedAt
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
    """, plots_to_insert)

    cur.executemany("""
        INSERT INTO LegalConclusions (
            ConclusionNumber, LandPlotId, LawyerId, NoArrestsOrMortgages, NoServitudes,
            BoundariesMatchDzk, SellerMatchesDrp, NoSanctions, RiskLevel, DzkExtractNumber,
            ConclusionText, IsApproved, CreatedAt
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
    """, conclusions_to_insert)

    cur.executemany("""
        INSERT INTO PlotDocuments (
            LandPlotId, Title, DocType, DocumentNumber, IssueDate, FileUrl,
            IsSignedByKep, KepSignatureHash, IsVerified
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?);
    """, docs_to_insert)

    conn.commit()
    conn.close()
    print(f"Successfully inserted {len(plots_to_insert)} land plots, {len(conclusions_to_insert)} legal conclusions, and {len(docs_to_insert)} verified documents.")

if __name__ == "__main__":
    main()
