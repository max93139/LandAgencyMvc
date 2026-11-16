**Лабораторна робота 5 (4 год.)**   
**Тема: CRUD, ACID, повнота функціоналу**   
**Мета: забезпечити повноту функціоналу розроблюваного проекту Короткі теоретичні відомості (додатково до лекційного матеріалу)** 

**Основні операції з даними**   
Більшість операцій з даними являють собою *CRUD-операції (Create, Read, Update, Delete)*, тобто отримання даних, створення, оновлення і видалення. **Entity Framework** дозволяє легко проводити такі операції. 

Для прикладів з операціями візьмемо модель **Phone**:   
*public class Phone*   
*{*   
 *public int Id {get; set; }*   
 *public string Name {get; set; }*   
 *public int Price {get; set; }*   
*}* 

І наступний клас контексту даних:   
*public class PhoneContext: DbContext*   
*{*   
 *public PhoneContext (): base ( "DefaultConnection")*   
 *{}*   
    
 *public DbSet \<Phone\> Phones {get; set; }*   
*}* 

**Додавання**   
Для додавання застосовується метод **Add** () у об'єкта **DbSet**:   
*using (PhoneContext db \= new PhoneContext())*   
*{*   
 *Phone p1 \= new Phone {Name \= "Samsung Galaxy S7", Price \= 20000};  Phone p2 \= new Phone {Name \= "iPhone 7", Price \= 28000};* 

*// додавання*   
 *db.Phones.Add (p1);*   
 *db.Phones.Add (p2);*   
 *db.SaveChanges ();*    
*// збереження змін* 

 *var phones \= db.Phones.ToList ();*   
 *foreach (var p in phones)*   
 *Console.WriteLine ( "{0} \- {1} \- {2}", p.Id, p.Name, p.Price);*   
*}* 

Після додавання потрібно зберегти всі зміни за допомогою методу **SaveChanges**(). 

**Редагування**   
Контекст даних здатний відстежувати зміни об'єктів, тому для редагування об'єкта досить змінити його властивості й після цього викликати метод **SaveChanges**(): *using (PhoneContext db \= new PhoneContext ())* 

*{*   
 *// отримуємо перший об'єкт*  
 *Phone p1 \= db.Phones.FirstOrDefault ();* 

 *p1.Price \= 30000;*   
 *db.SaveChanges ();*    
*// зберігаємо зміни*   
*}* 

Але розглянемо іншу ситуацію:   
*Phone p1;*   
*using (PhoneContext db \= new PhoneContext ())*   
*{*   
 *p1 \= db.Phones.FirstOrDefault ();*   
*}* 

*using (PhoneContext db \= new PhoneContext ())*   
*{*   
 *if (p1\! \= null)*   
 *{*   
 *p1.Price \= 60000;*   
 *db.SaveChanges ();*   
 *}*   
*}* 

Так як об'єкт *Phone* отриманий в одному контексті, змінюється для іншого контексту, який його не відслідковує. В результаті зміни не будуть збережені. Щоб зміни збереглися, нам потрібно явно встановити для його стану значення **EntityState.Modified**: *using (PhoneContext db \= new PhoneContext ())* 

*{*   
 *if (p1\! \= null)*   
 *{*   
 *p1.Price \= 60000;*   
 *db.Entry (p1) .State \= EntityState.Modified;*   
 *db.SaveChanges ();*   
 *}*   
*}* 

**Вилучення**   
Для вилучення об'єкта застосовується метод **Remove**() об'єкта **DbSet**: *using (PhoneContext db \= new PhoneContext ())* 

*{*   
 *Phone p1 \= db.Phones.FirstOrDefault ();*   
 *if (p1\! \= null)*   
 *{*   
 *db.Phones.Remove (p1);*   
 *db.SaveChanges ();*   
 *}*   
*}* 

Але як і у випадку з оновленням тут ми можемо зіткнутися з подібною проблемою, коли об'єкт отримуємо з бази даних в межах одного контексту, а намагаємося вилучити в іншому контексті. І в цьому випадку нам потрібно вручну встановлювати у об'єкта стан **EntityState.Deleted**:  
*using (PhoneContext db \= new PhoneContext ())*   
*{*   
 *if (p1\! \= null)*   
 *{*   
 *db.Entry (p1) .State \= EntityState.Deleted;*   
 *db.SaveChanges ();*   
 *}*   
*}*   
**Метод Attach**   
Якщо об'єкт отриманий в одному контексті, зберігається в іншому, то ми можемо встановлювати у нього вручну стан **EntityState.Updated** або **EntityState.Deleted**. Але є ще один спосіб: за допомогою методу **Attach** у об'єкта **DbSet** ми можемо прикріплювати об'єкт до поточного контексту даних 

**Завдання**   
1\. Переглянути проект і перевірити функціонал на повноту згідно з оголошеним раніше (див. ЛР 1-4). У випадку необхідності – доопрацювати його.  

2\. Перевірити можливість проведення CRUD-операцій *(Create, Read, Update, Delete)* для всіх даних проекту.  

3\. Перевірити, щоб усі операції з базою даних відповідали властивостям ACID (*Atomicity, Consistency, Isolation, Durability*).  

4\. Перевірити, чи реалізовано ролі.  

**Перелік використаних джерел**  

1\. Mark J. Price. C\# 11 and .NET 7 – Modern Cross-Platform Development Fundamentals. 7th Edition. Copyright © 2022 Packt Publishing. ISBN: 978-1- 80323-780-0 

2\. Настенко Д. В., Нестерко А. Б. Об’єктно-орієнтоване програмування. Частина 1\. Основи об’єктно-орієнтованого програмування на мові С\#: навчальний посібник. К.: НТУУ «КПІ», 2016\.  76 с.  

**Інформаційні ресурси**  

1\. Документація C: https\://docs.microsoft.com/en-us/dotnet/csharp/