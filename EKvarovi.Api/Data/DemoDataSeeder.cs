using System.IO.Compression;
using System.Text;
using EKvarovi.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Data;

// Bogat demo seed (lokacije, zaposlenici, materijali, prijave kroz SVE statuse, dodjele,
// intervencije, povijest) tako da netko tko PRVI PUT pokrene aplikaciju odmah vidi Dashboard,
// SLA izvještaj, grafove i timeline popunjene smislenim podacima - ne praznu bazu ni "test 1/2/3".
// Mora se pokrenuti PRIJE DemoUserSeeder-a: tehnicar@/prijavitelj@ AppUser racuni se vezuju na
// KONKRETNE Employee zapise iz ovog seeda preko Email markera (vidi TechnicianMarkerEmail/
// ReporterMarkerEmail), pa ti Employee zapisi moraju vec postojati kad se DemoUserSeeder izvrsi.
public static class DemoDataSeeder
{
    // Markeri preko kojih DemoUserSeeder pronalazi TOCNO ODREDJENI Employee zapis za
    // tehnicar@/prijavitelj@ demo racune - eksplicitno i predvidljivo, umjesto krhkog
    // "prvi pronadjeni po ulozi" pristupa.
    internal const string TechnicianMarkerEmail = "tehnicar@ekvarovi.hr";
    internal const string ReporterMarkerEmail = "prijavitelj@ekvarovi.hr";

    // Privremeni placeholder AppUserId dok JWT autentikacija ne pokriva seed kontekst - isti
    // sistemski racun (Id=1, "Sistem") koji WorkAssignmentsController/AttachmentsController
    // koriste kao AssignedByAppUserId/UploadedByAppUserId placeholder.
    private const int SystemAppUserId = 1;

    // Id-jevi lookup sifrarnika - moraju odgovarati EKvaroviDbContext.SeedLookups HasData.
    private const int LocationTypeUpravnaZgrada = 1;
    private const int LocationTypeSkola = 2;
    private const int LocationTypeZdravstvena = 3;
    private const int LocationTypeSkladiste = 4;

    private const int FaultTypeElektrika = 1;
    private const int FaultTypeVoda = 2;
    private const int FaultTypeGrijanje = 3;
    private const int FaultTypeMreza = 4;
    private const int FaultTypeGradevinski = 5;
    private const int FaultTypeOstalo = 6;

    private const int PriorityNizak = 1;
    private const int PrioritySrednji = 2;
    private const int PriorityVisok = 3;
    private const int PriorityKritican = 4;

    private const int StatusZaprimljeno = 1;
    private const int StatusPregledano = 2;
    private const int StatusDodijeljeno = 3;
    private const int StatusURadu = 4;
    private const int StatusRijeseno = 5;
    private const int StatusZatvoreno = 6;

    private const int IntStatusUTijeku = 2;
    private const int IntStatusZavrsena = 3;
    private const int IntStatusNeuspjesna = 4;

    private const int UnitKomad = 1;
    private const int UnitMetar = 2;
    private const int UnitLitra = 3;
    private const int UnitKilogram = 4;
    private const int UnitPaket = 5;

    private const int PurposeFotografijaPrije = 1;
    private const int PurposeFotografijaPoslije = 2;

    private const string NameZaprimljeno = "Zaprimljeno";
    private const string NamePregledano = "Pregledano";
    private const string NameDodijeljeno = "Dodijeljeno";
    private const string NameURadu = "U radu";
    private const string NameRijeseno = "Riješeno";
    private const string NameZatvoreno = "Zatvoreno";

    public static async Task SeedAsync(EKvaroviDbContext db, string contentRootPath)
    {
        // Zastita od dupliciranja na svaki restart - isti princip kao DemoUserSeeder,
        // samo na razini cijelog demo skupa podataka (dovoljno provjeriti jednu tablicu).
        if (await db.Locations.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;

        var locations = await SeedLocationsAsync(db, now);
        var employees = await SeedEmployeesAsync(db, locations, now);
        var materials = await SeedMaterialsAsync(db);
        await SeedFaultReportsAsync(db, locations, employees, materials, now, contentRootPath);
    }

    private static async Task<(Location L1, Location L2, Location L3, Location L4, Location L5, Location L6, Location L7, Location L8)>
        SeedLocationsAsync(EKvaroviDbContext db, DateTime now)
    {
        var createdAt = now.AddDays(-200);

        var l1 = new Location { Name = "Upravna zgrada Grada", Address = "Obala kneza Domagoja 1, Split", LocationTypeId = LocationTypeUpravnaZgrada, IsActive = true, CreatedAt = createdAt };
        var l2 = new Location { Name = "Upravna zgrada Županije", Address = "Domovinskog rata 2, Split", LocationTypeId = LocationTypeUpravnaZgrada, IsActive = true, CreatedAt = createdAt };
        var l3 = new Location { Name = "Osnovna škola Ivana Gorana Kovačića", Address = "Matoševa 15, Split", LocationTypeId = LocationTypeSkola, IsActive = true, CreatedAt = createdAt };
        var l4 = new Location { Name = "Srednja škola Tehnička", Address = "Ruđera Boškovića 22, Split", LocationTypeId = LocationTypeSkola, IsActive = true, CreatedAt = createdAt };
        var l5 = new Location { Name = "Dom zdravlja Centar", Address = "Spinčićeva 1, Split", LocationTypeId = LocationTypeZdravstvena, IsActive = true, CreatedAt = createdAt };
        var l6 = new Location { Name = "Poliklinika Istok", Address = "Vukovarska 46, Split", LocationTypeId = LocationTypeZdravstvena, IsActive = true, CreatedAt = createdAt };
        var l7 = new Location { Name = "Skladište Županije - Sjever", Address = "Kopilica 5, Split", LocationTypeId = LocationTypeSkladiste, IsActive = true, CreatedAt = createdAt };
        // Namjerno neaktivna - demo/edge-case za pravilo "prijava mora pripadati aktivnoj lokaciji"
        // (forma je ne bi smjela ni ponuditi, ali korisna je za rucno testiranje odbijanja na API-ju).
        var l8 = new Location { Name = "Upravna zgrada - Podružnica Jug", Address = "Poljička cesta 100, Split", LocationTypeId = LocationTypeUpravnaZgrada, IsActive = false, CreatedAt = createdAt };

        db.Locations.AddRange(l1, l2, l3, l4, l5, l6, l7, l8);
        await db.SaveChangesAsync();

        return (l1, l2, l3, l4, l5, l6, l7, l8);
    }

    private static async Task<(
        Employee Petra, Employee Ivan, Employee Ana, Employee Tomislav, Employee Marija, Employee Stjepan,
        Employee Luka, Employee Petar, Employee Josip, Employee Domagoj, Employee Sara, Employee Filip)>
        SeedEmployeesAsync(
            EKvaroviDbContext db,
            (Location L1, Location L2, Location L3, Location L4, Location L5, Location L6, Location L7, Location L8) loc,
            DateTime now)
    {
        var createdAt = now.AddDays(-190);

        var petra = new Employee { LocationId = loc.L1.Id, FirstName = "Petra", LastName = "Horvat", Email = "petra.horvat@zupanija.hr", IsReporter = true, IsTechnician = false, IsActive = true, CreatedAt = createdAt };
        var ivan = new Employee { LocationId = loc.L1.Id, FirstName = "Ivan", LastName = "Kovačević", Email = "ivan.kovacevic@zupanija.hr", IsReporter = false, IsTechnician = true, IsActive = true, CreatedAt = createdAt };
        var ana = new Employee { LocationId = loc.L2.Id, FirstName = "Ana", LastName = "Marić", Email = "ana.maric@zupanija.hr", IsReporter = true, IsTechnician = false, IsActive = true, CreatedAt = createdAt };
        var tomislav = new Employee { LocationId = loc.L2.Id, FirstName = "Tomislav", LastName = "Babić", Email = "tomislav.babic@zupanija.hr", IsReporter = true, IsTechnician = true, IsActive = true, CreatedAt = createdAt };
        var marija = new Employee { LocationId = loc.L3.Id, FirstName = "Marija", LastName = "Novak", Email = "marija.novak@zupanija.hr", IsReporter = true, IsTechnician = false, IsActive = true, CreatedAt = createdAt };
        var stjepan = new Employee { LocationId = loc.L4.Id, FirstName = "Stjepan", LastName = "Perić", Email = "stjepan.peric@zupanija.hr", IsReporter = true, IsTechnician = false, IsActive = true, CreatedAt = createdAt };
        var luka = new Employee { LocationId = loc.L5.Id, FirstName = "Luka", LastName = "Jurić", Email = "luka.juric@zupanija.hr", IsReporter = true, IsTechnician = false, IsActive = true, CreatedAt = createdAt };
        var petar = new Employee { LocationId = loc.L6.Id, FirstName = "Petar", LastName = "Vuković", Email = "petar.vukovic@zupanija.hr", IsReporter = true, IsTechnician = false, IsActive = true, CreatedAt = createdAt };
        var josip = new Employee { LocationId = loc.L7.Id, FirstName = "Josip", LastName = "Radić", Email = "josip.radic@zupanija.hr", IsReporter = true, IsTechnician = true, IsActive = true, CreatedAt = createdAt };
        // Marker zaposlenici - DemoUserSeeder ih pronalazi preko Email polja (TechnicianMarkerEmail/
        // ReporterMarkerEmail), pa prijava kao tehnicar@/prijavitelj@ UVIJEK vidi istu, predvidljivu
        // kolicinu podataka na MyAssignments/MyReports.
        var domagoj = new Employee { LocationId = loc.L1.Id, FirstName = "Domagoj", LastName = "Perković", Email = TechnicianMarkerEmail, IsReporter = false, IsTechnician = true, IsActive = true, CreatedAt = createdAt };
        var sara = new Employee { LocationId = loc.L3.Id, FirstName = "Sara", LastName = "Kralj", Email = ReporterMarkerEmail, IsReporter = true, IsTechnician = false, IsActive = true, CreatedAt = createdAt };
        var filip = new Employee { LocationId = loc.L5.Id, FirstName = "Filip", LastName = "Matić", Email = "filip.matic@zupanija.hr", IsReporter = false, IsTechnician = true, IsActive = true, CreatedAt = createdAt };

        db.Employees.AddRange(petra, ivan, ana, tomislav, marija, stjepan, luka, petar, josip, domagoj, sara, filip);
        await db.SaveChangesAsync();

        return (petra, ivan, ana, tomislav, marija, stjepan, luka, petar, josip, domagoj, sara, filip);
    }

    private static async Task<(
        Material Kabel, Material Vijci, Material Boja, Material Cement, Material Filter,
        Material SijalicaLed, Material CijevPvc, Material BrtvenaMasa)> SeedMaterialsAsync(EKvaroviDbContext db)
    {
        var kabel = new Material { Name = "Kabel", MaterialUnitId = UnitMetar, IsActive = true };
        var vijci = new Material { Name = "Vijci", MaterialUnitId = UnitKomad, IsActive = true };
        var boja = new Material { Name = "Boja", MaterialUnitId = UnitLitra, IsActive = true };
        var cement = new Material { Name = "Cement", MaterialUnitId = UnitKilogram, IsActive = true };
        var filter = new Material { Name = "Filter", MaterialUnitId = UnitPaket, IsActive = true };
        var sijalica = new Material { Name = "Sijalica LED", MaterialUnitId = UnitKomad, IsActive = true };
        var cijevPvc = new Material { Name = "Cijev PVC", MaterialUnitId = UnitMetar, IsActive = true };
        var brtvenaMasa = new Material { Name = "Brtvena masa", MaterialUnitId = UnitLitra, IsActive = true };

        db.Materials.AddRange(kabel, vijci, boja, cement, filter, sijalica, cijevPvc, brtvenaMasa);
        await db.SaveChangesAsync();

        return (kabel, vijci, boja, cement, filter, sijalica, cijevPvc, brtvenaMasa);
    }

    private static async Task SeedFaultReportsAsync(
        EKvaroviDbContext db,
        (Location L1, Location L2, Location L3, Location L4, Location L5, Location L6, Location L7, Location L8) loc,
        (Employee Petra, Employee Ivan, Employee Ana, Employee Tomislav, Employee Marija, Employee Stjepan,
            Employee Luka, Employee Petar, Employee Josip, Employee Domagoj, Employee Sara, Employee Filip) emp,
        (Material Kabel, Material Vijci, Material Boja, Material Cement, Material Filter,
            Material SijalicaLed, Material CijevPvc, Material BrtvenaMasa) mat,
        DateTime now,
        string contentRootPath)
    {
        // === 1. FaultReports (finalno stanje polja) - status/tip/prioritet/rok postavljeni
        // izravno na krajnju vrijednost, a cijeli "kako je do toga doslo" tok gradi se ispod
        // kroz WorkAssignments/Interventions i povijest (isti pristup kao DemoUserSeeder:
        // izravna izgradnja entiteta, ne poziv kroz kontrolere/DTO-e). ===

        // -- Zaprimljeno (3) - bez tipa/prioriteta/roka, cekaju pregled Upravitelja --
        var fr01 = NewReport(loc.L1.Id, emp.Petra.Id, "Ne radi rasvjeta u hodniku drugog kata.", null, null, StatusZaprimljeno, null, now.AddHours(-18), now.AddHours(-18));
        var fr02 = NewReport(loc.L3.Id, emp.Sara.Id, "Pukla je cijev u sanitarnom čvoru u prizemlju, voda curi po podu.", null, null, StatusZaprimljeno, null, now.AddHours(-6), now.AddHours(-6));
        var fr03 = NewReport(loc.L5.Id, emp.Luka.Id, "Radijatori u čekaonici prijema su hladni, grijanje ne radi.", null, null, StatusZaprimljeno, null, now.AddHours(-2), now.AddHours(-2));

        // -- Pregledano (2) - tip/prioritet/rok postavljeni, jos nema dodjele --
        var fr04CreatedAt = now.AddDays(-2);
        var fr04ReviewedAt = fr04CreatedAt.AddHours(6);
        var fr04 = NewReport(loc.L2.Id, emp.Ana.Id, "Curi slavina u kuhinji na katu, potrebna zamjena brtve.", FaultTypeVoda, PrioritySrednji, StatusPregledano, now.AddDays(5), fr04CreatedAt, fr04ReviewedAt);

        var fr05CreatedAt = now.AddDays(-1);
        var fr05ReviewedAt = fr05CreatedAt.AddHours(4);
        var fr05 = NewReport(loc.L3.Id, emp.Sara.Id, "Kratki spoj u razvodnom ormariću, povremeno nestaje struja u učionici.", FaultTypeElektrika, PriorityVisok, StatusPregledano, now.AddHours(36), fr05CreatedAt, fr05ReviewedAt);

        // -- Dodijeljeno (4) - aktivna dodjela postoji, intervencija jos nije pokrenuta --
        var fr06CreatedAt = now.AddDays(-5);
        var fr06ReviewedAt = fr06CreatedAt.AddHours(5);
        var fr06AssignedAt = fr06CreatedAt.AddHours(10);
        // Kritican prioritet s rokom koji je VEC PROSAO - demo "Zakasnjele" pokazatelja na Dashboardu.
        var fr06 = NewReport(loc.L1.Id, emp.Petra.Id, "Ispao je osigurač na cijelom katu, rasvjeta i utičnice ne rade.", FaultTypeElektrika, PriorityKritican, StatusDodijeljeno, now.AddDays(-2), fr06CreatedAt, fr06AssignedAt);

        var fr07CreatedAt = now.AddDays(-4);
        var fr07ReviewedAt = fr07CreatedAt.AddHours(3);
        var fr07FirstAssignedAt = fr07CreatedAt.AddHours(8);
        var fr07ReassignAt = now.AddDays(-1);
        var fr07 = NewReport(loc.L4.Id, emp.Stjepan.Id, "Kotlovnica ne grije, temperatura u učionicama pada.", FaultTypeGrijanje, PriorityVisok, StatusDodijeljeno, now.AddDays(7), fr07CreatedAt, fr07ReassignAt);

        var fr08CreatedAt = now.AddDays(-3);
        var fr08ReviewedAt = fr08CreatedAt.AddHours(4);
        var fr08AssignedAt = fr08CreatedAt.AddHours(9);
        var fr08 = NewReport(loc.L6.Id, emp.Petar.Id, "Nema internetske veze u cijeloj zgradi, mrežni preklopnik vjerojatno neispravan.", FaultTypeMreza, PrioritySrednji, StatusDodijeljeno, now.AddDays(10), fr08CreatedAt, fr08AssignedAt);

        var fr09CreatedAt = now.AddDays(-2);
        var fr09ReviewedAt = fr09CreatedAt.AddHours(3);
        var fr09AssignedAt = fr09CreatedAt.AddHours(7);
        var fr09 = NewReport(loc.L7.Id, emp.Josip.Id, "Vrata skladišnog boksa br. 3 se ne mogu zaključati, oštećena brava.", FaultTypeGradevinski, PriorityNizak, StatusDodijeljeno, now.AddDays(14), fr09CreatedAt, fr09AssignedAt);

        // -- U radu (2) - aktivna intervencija u tijeku --
        var fr10CreatedAt = now.AddDays(-6);
        var fr10ReviewedAt = fr10CreatedAt.AddHours(5);
        var fr10AssignedAt = fr10CreatedAt.AddDays(1);
        var fr10IntStart = now.AddHours(-2);
        // Rok unutar sljedecih 24h - demo "Rok uskoro istice" pokazatelja.
        var fr10 = NewReport(loc.L2.Id, emp.Ana.Id, "Curi cijev ispod sudopera u čajnoj kuhinji, voda se skuplja na podu.", FaultTypeVoda, PriorityVisok, StatusURadu, now.AddHours(10), fr10CreatedAt, fr10IntStart);

        var fr11CreatedAt = now.AddDays(-10);
        var fr11ReviewedAt = fr11CreatedAt.AddHours(6);
        var fr11AssignedAt = fr11CreatedAt.AddDays(1);
        var fr11IntStart = now.AddDays(-1);
        var fr11 = NewReport(loc.L5.Id, emp.Luka.Id, "Grijanje u čekaonici radi s prekidima, potrebna provjera sustava.", FaultTypeGrijanje, PrioritySrednji, StatusURadu, now.AddDays(20), fr11CreatedAt, fr11IntStart);

        // -- Riješeno (2) - uspjesna intervencija zavrsena, ceka zatvaranje --
        var fr12CreatedAt = now.AddDays(-4);
        var fr12ReviewedAt = fr12CreatedAt.AddHours(4);
        var fr12AssignedAt = fr12CreatedAt.AddHours(9);
        var fr12IntStart = now.AddDays(-3);
        var fr12IntEnd = now.AddHours(-12);
        var fr12 = NewReport(loc.L1.Id, emp.Petra.Id, "Rasklopna kutija na drugom katu iskri, hitno potrebna intervencija.", FaultTypeElektrika, PriorityKritican, StatusRijeseno, now.AddDays(-1), fr12CreatedAt, fr12IntEnd);

        var fr13CreatedAt = now.AddDays(-3);
        var fr13ReviewedAt = fr13CreatedAt.AddHours(5);
        var fr13AssignedAt = fr13CreatedAt.AddHours(10);
        var fr13IntStart = now.AddDays(-2);
        var fr13IntEnd = now.AddDays(-1);
        var fr13 = NewReport(loc.L3.Id, emp.Sara.Id, "Ventil na glavnom vodovodnom priključku propušta vodu.", FaultTypeVoda, PrioritySrednji, StatusRijeseno, now.AddDays(2), fr13CreatedAt, fr13IntEnd);

        // -- Zatvoreno (5) - cijeli tok odradjen, CreatedAt rasporedjen kroz zadnja ~3 mjeseca --
        var fr14CreatedAt = now.AddDays(-85);
        var fr14ReviewedAt = fr14CreatedAt.AddHours(6);
        var fr14AssignedAt = fr14CreatedAt.AddDays(1);
        var fr14DueDate = fr14CreatedAt.AddDays(5);
        var fr14IntStart = fr14CreatedAt.AddDays(2);
        var fr14IntEnd = fr14CreatedAt.AddDays(4);
        var fr14ClosedAt = fr14IntEnd.AddDays(1);
        var fr14 = NewReport(loc.L2.Id, emp.Tomislav.Id, "Prekidač za rasvjetu u sobi 214 ne radi ispravno.", FaultTypeElektrika, PriorityVisok, StatusZatvoreno, fr14DueDate, fr14CreatedAt, fr14ClosedAt);

        var fr15CreatedAt = now.AddDays(-60);
        var fr15ReviewedAt = fr15CreatedAt.AddHours(5);
        var fr15AssignedAt = fr15CreatedAt.AddHours(12);
        var fr15DueDate = fr15CreatedAt.AddDays(3);
        var fr15IntAStart = fr15CreatedAt.AddDays(1);
        var fr15IntAEnd = fr15IntAStart.AddHours(3);
        var fr15IntBStart = fr15CreatedAt.AddDays(4);
        var fr15IntBEnd = fr15IntBStart.AddHours(5);
        var fr15ClosedAt = fr15IntBEnd.AddDays(1);
        // NAJVAZNIJI scenarij specifikacije: neuspjesna pa nova (uspjesna) intervencija na ISTOJ dodjeli.
        var fr15 = NewReport(loc.L4.Id, emp.Stjepan.Id, "Kotlovnica povremeno gasi grijanje, termostat vjerojatno neispravan.", FaultTypeGrijanje, PriorityKritican, StatusZatvoreno, fr15DueDate, fr15CreatedAt, fr15ClosedAt);

        var fr16CreatedAt = now.AddDays(-45);
        var fr16ReviewedAt = fr16CreatedAt.AddHours(4);
        var fr16AssignedAt = fr16CreatedAt.AddDays(1);
        var fr16DueDate = fr16CreatedAt.AddDays(10);
        var fr16IntStart = fr16CreatedAt.AddDays(3);
        var fr16IntEnd = fr16IntStart.AddHours(2);
        var fr16ClosedAt = fr16IntEnd.AddDays(2);
        var fr16 = NewReport(loc.L3.Id, emp.Sara.Id, "Slavina u čajnoj kuhinji škole je puknula, voda curi neprekidno.", FaultTypeVoda, PriorityNizak, StatusZatvoreno, fr16DueDate, fr16CreatedAt, fr16ClosedAt);

        var fr17CreatedAt = now.AddDays(-30);
        var fr17ReviewedAt = fr17CreatedAt.AddHours(3);
        var fr17AssignedAt = fr17CreatedAt.AddHours(8);
        var fr17DueDate = fr17CreatedAt.AddDays(2);
        var fr17IntStart = fr17CreatedAt.AddDays(1);
        var fr17IntEnd = fr17IntStart.AddHours(1);
        var fr17ClosedAt = fr17IntEnd.AddDays(1);
        var fr17 = NewReport(loc.L6.Id, emp.Petar.Id, "Mrežni preklopnik u serverskoj sobi je izgorio, cijela poliklinika bez interneta.", FaultTypeMreza, PrioritySrednji, StatusZatvoreno, fr17DueDate, fr17CreatedAt, fr17ClosedAt);

        var fr18CreatedAt = now.AddDays(-15);
        var fr18ReviewedAt = fr18CreatedAt.AddHours(5);
        var fr18AssignedAt = fr18CreatedAt.AddHours(10);
        var fr18DueDate = fr18CreatedAt.AddDays(7);
        var fr18IntStart = fr18CreatedAt.AddDays(2);
        var fr18IntEnd = fr18IntStart.AddHours(3);
        var fr18ClosedAt = fr18IntEnd.AddDays(3);
        var fr18 = NewReport(loc.L7.Id, emp.Josip.Id, "Vrata skladišnog boksa br. 3 ne mogu se zaključati, brava oštećena.", FaultTypeOstalo, PriorityNizak, StatusZatvoreno, fr18DueDate, fr18CreatedAt, fr18ClosedAt);

        var allReports = new[] { fr01, fr02, fr03, fr04, fr05, fr06, fr07, fr08, fr09, fr10, fr11, fr12, fr13, fr14, fr15, fr16, fr17, fr18 };
        db.FaultReports.AddRange(allReports);
        await db.SaveChangesAsync();

        // === 2. WorkAssignments (dodjele) - jedna po prijavi od "Dodijeljeno" nadalje,
        // osim fr07 koja ima POVIJEST reassignmenta (stara neaktivna + nova aktivna dodjela). ===
        var wa06 = NewAssignment(fr06.Id, emp.Domagoj.Id, fr06AssignedAt, true, null, null);

        var wa07Old = NewAssignment(fr07.Id, emp.Filip.Id, fr07FirstAssignedAt, false, fr07ReassignAt, null);
        var wa07New = NewAssignment(fr07.Id, emp.Domagoj.Id, fr07ReassignAt, true, null, "Filip na godišnjem odmoru, preuzima Domagoj Perković.");

        var wa08 = NewAssignment(fr08.Id, emp.Ivan.Id, fr08AssignedAt, true, null, null);
        var wa09 = NewAssignment(fr09.Id, emp.Tomislav.Id, fr09AssignedAt, true, null, null);
        var wa10 = NewAssignment(fr10.Id, emp.Ivan.Id, fr10AssignedAt, true, null, null);
        var wa11 = NewAssignment(fr11.Id, emp.Filip.Id, fr11AssignedAt, true, null, null);
        var wa12 = NewAssignment(fr12.Id, emp.Domagoj.Id, fr12AssignedAt, true, null, null);
        var wa13 = NewAssignment(fr13.Id, emp.Josip.Id, fr13AssignedAt, true, null, null);
        var wa14 = NewAssignment(fr14.Id, emp.Ivan.Id, fr14AssignedAt, true, null, null);
        var wa15 = NewAssignment(fr15.Id, emp.Tomislav.Id, fr15AssignedAt, true, null, null);
        var wa16 = NewAssignment(fr16.Id, emp.Filip.Id, fr16AssignedAt, true, null, null);
        var wa17 = NewAssignment(fr17.Id, emp.Josip.Id, fr17AssignedAt, true, null, null);
        var wa18 = NewAssignment(fr18.Id, emp.Domagoj.Id, fr18AssignedAt, true, null, null);

        db.WorkAssignments.AddRange(wa06, wa07Old, wa07New, wa08, wa09, wa10, wa11, wa12, wa13, wa14, wa15, wa16, wa17, wa18);
        await db.SaveChangesAsync();

        // === 3. Interventions ===
        var i10 = NewIntervention(wa10.Id, IntStatusUTijeku, fr10IntStart, null, null, null);
        var i11 = NewIntervention(wa11.Id, IntStatusUTijeku, fr11IntStart, null, null, null);

        var i12 = NewIntervention(wa12.Id, IntStatusZavrsena, fr12IntStart, fr12IntEnd, DurationMinutes(fr12IntStart, fr12IntEnd),
            "Zamijenjena rasklopna kutija na 2. katu, sustav testiran i ispravan.");
        var i13 = NewIntervention(wa13.Id, IntStatusZavrsena, fr13IntStart, fr13IntEnd, DurationMinutes(fr13IntStart, fr13IntEnd),
            "Zamijenjena brtva na ventilu, curenje sanirano.");
        var i14 = NewIntervention(wa14.Id, IntStatusZavrsena, fr14IntStart, fr14IntEnd, DurationMinutes(fr14IntStart, fr14IntEnd),
            "Popravljen prekidač u sobi 214.");

        // Neuspjesna pa nova uspjesna intervencija na ISTOJ dodjeli (wa15).
        var i15a = NewIntervention(wa15.Id, IntStatusNeuspjesna, fr15IntAStart, fr15IntAEnd, DurationMinutes(fr15IntAStart, fr15IntAEnd),
            "Zamijenjen termostat, no problem se ponovno pojavio nakon par sati - potrebna dodatna dijagnostika kotlovnice.");
        var i15b = NewIntervention(wa15.Id, IntStatusZavrsena, fr15IntBStart, fr15IntBEnd, DurationMinutes(fr15IntBStart, fr15IntBEnd),
            "Pronađen i zamijenjen neispravan razdjelnik grijanja, sustav testiran tijekom cijelog dana i radi ispravno.");

        var i16 = NewIntervention(wa16.Id, IntStatusZavrsena, fr16IntStart, fr16IntEnd, DurationMinutes(fr16IntStart, fr16IntEnd),
            "Zamijenjena slavina u čajnoj kuhinji.");
        var i17 = NewIntervention(wa17.Id, IntStatusZavrsena, fr17IntStart, fr17IntEnd, DurationMinutes(fr17IntStart, fr17IntEnd),
            "Zamijenjen mrežni preklopnik (switch) u serverskoj sobi.");
        var i18 = NewIntervention(wa18.Id, IntStatusZavrsena, fr18IntStart, fr18IntEnd, DurationMinutes(fr18IntStart, fr18IntEnd),
            "Popravljena vrata skladišnog boksa br. 3, ugrađena nova brava.");

        db.Interventions.AddRange(i10, i11, i12, i13, i14, i15a, i15b, i16, i17, i18);
        await db.SaveChangesAsync();

        // === 4. InterventionMaterials (materijal i kolicina na nekoliko zavrsenih intervencija) ===
        db.InterventionMaterials.AddRange(
            new InterventionMaterial { InterventionId = i12.Id, MaterialId = mat.Kabel.Id, Quantity = 8m },
            new InterventionMaterial { InterventionId = i12.Id, MaterialId = mat.Vijci.Id, Quantity = 12m },
            new InterventionMaterial { InterventionId = i13.Id, MaterialId = mat.CijevPvc.Id, Quantity = 3m },
            new InterventionMaterial { InterventionId = i15b.Id, MaterialId = mat.Vijci.Id, Quantity = 10m },
            new InterventionMaterial { InterventionId = i15b.Id, MaterialId = mat.BrtvenaMasa.Id, Quantity = 0.5m },
            new InterventionMaterial { InterventionId = i16.Id, MaterialId = mat.CijevPvc.Id, Quantity = 5m },
            new InterventionMaterial { InterventionId = i16.Id, MaterialId = mat.Vijci.Id, Quantity = 4m },
            new InterventionMaterial { InterventionId = i17.Id, MaterialId = mat.Kabel.Id, Quantity = 15m });

        // === 5. Attachments - 2-3 male placeholder PNG slike, generirane u runtimeu (bez vanjske
        // slikovne biblioteke) i fizicki spremljene u wwwroot/uploads s generiranim GUID imenom,
        // isti obrazac kao AttachmentsController.UploadAttachment. ===
        var uploadsFolder = Path.Combine(contentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var prijeFotka12 = await SaveSeedImageAsync(uploadsFolder, 176, 60, 60);
        var posljeFotka12 = await SaveSeedImageAsync(uploadsFolder, 76, 175, 80);
        var prijeFotka15 = await SaveSeedImageAsync(uploadsFolder, 230, 160, 40);

        db.Attachments.AddRange(
            new Attachment
            {
                FaultReportId = fr12.Id,
                InterventionId = i12.Id,
                AttachmentPurposeId = PurposeFotografijaPrije,
                OriginalFileName = "prije-popravka.png",
                StoredFileName = prijeFotka12.StoredFileName,
                ContentType = "image/png",
                FileSizeBytes = prijeFotka12.SizeBytes,
                UploadedAt = fr12IntStart,
                UploadedByAppUserId = SystemAppUserId
            },
            new Attachment
            {
                FaultReportId = fr12.Id,
                InterventionId = i12.Id,
                AttachmentPurposeId = PurposeFotografijaPoslije,
                OriginalFileName = "poslije-popravka.png",
                StoredFileName = posljeFotka12.StoredFileName,
                ContentType = "image/png",
                FileSizeBytes = posljeFotka12.SizeBytes,
                UploadedAt = fr12IntEnd,
                UploadedByAppUserId = SystemAppUserId
            },
            new Attachment
            {
                FaultReportId = fr15.Id,
                InterventionId = i15a.Id,
                AttachmentPurposeId = PurposeFotografijaPrije,
                OriginalFileName = "kotlovnica-prije.png",
                StoredFileName = prijeFotka15.StoredFileName,
                ContentType = "image/png",
                FileSizeBytes = prijeFotka15.SizeBytes,
                UploadedAt = fr15IntAStart,
                UploadedByAppUserId = SystemAppUserId
            });

        // === 6. FaultReportHistoryEvents - vremenska crta za SVAKU prijavu, ista tvornica i
        // isti EventType nazivi koje koriste FaultReportsController/WorkAssignmentsController/
        // InterventionsController, tako da "Vremenska crta" izgleda identicno stvarnom toku. ===
        var history = new List<FaultReportHistoryEvent>();

        void Created(FaultReport fr, DateTime at) => history.Add(FaultReportHistoryEvents.Create(fr.Id, "StatusChanged", null, NameZaprimljeno, SystemAppUserId, at));
        void Reviewed(FaultReport fr, string typeName, string priorityName, DateTime at)
        {
            history.Add(FaultReportHistoryEvents.Create(fr.Id, "TypeSet", null, typeName, SystemAppUserId, at));
            history.Add(FaultReportHistoryEvents.Create(fr.Id, "PriorityChanged", null, priorityName, SystemAppUserId, at));
            history.Add(FaultReportHistoryEvents.Create(fr.Id, "StatusChanged", NameZaprimljeno, NamePregledano, SystemAppUserId, at));
        }
        void Assigned(FaultReport fr, string technicianName, DateTime at)
        {
            history.Add(FaultReportHistoryEvents.Create(fr.Id, "Assigned", null, technicianName, SystemAppUserId, at));
            history.Add(FaultReportHistoryEvents.Create(fr.Id, "StatusChanged", NamePregledano, NameDodijeljeno, SystemAppUserId, at));
        }
        void Reassigned(FaultReport fr, string oldName, string newName, DateTime at)
            => history.Add(FaultReportHistoryEvents.Create(fr.Id, "Reassigned", oldName, newName, SystemAppUserId, at));
        void InterventionStarted(FaultReport fr, DateTime at)
            => history.Add(FaultReportHistoryEvents.Create(fr.Id, "StatusChanged", NameDodijeljeno, NameURadu, SystemAppUserId, at));
        void Resolved(FaultReport fr, DateTime at)
            => history.Add(FaultReportHistoryEvents.Create(fr.Id, "StatusChanged", NameURadu, NameRijeseno, SystemAppUserId, at));
        void Closed(FaultReport fr, DateTime at)
            => history.Add(FaultReportHistoryEvents.Create(fr.Id, "StatusChanged", NameRijeseno, NameZatvoreno, SystemAppUserId, at));

        // Zaprimljeno
        Created(fr01, fr01.CreatedAt);
        Created(fr02, fr02.CreatedAt);
        Created(fr03, fr03.CreatedAt);

        // Pregledano
        Created(fr04, fr04CreatedAt); Reviewed(fr04, "Voda", "Srednji", fr04ReviewedAt);
        Created(fr05, fr05CreatedAt); Reviewed(fr05, "Elektrika", "Visok", fr05ReviewedAt);

        // Dodijeljeno
        Created(fr06, fr06CreatedAt); Reviewed(fr06, "Elektrika", "Kritičan", fr06ReviewedAt); Assigned(fr06, "Domagoj Perković", fr06AssignedAt);

        Created(fr07, fr07CreatedAt); Reviewed(fr07, "Grijanje", "Visok", fr07ReviewedAt);
        Assigned(fr07, "Filip Matić", fr07FirstAssignedAt);
        Reassigned(fr07, "Filip Matić", "Domagoj Perković", fr07ReassignAt);

        Created(fr08, fr08CreatedAt); Reviewed(fr08, "Mreža", "Srednji", fr08ReviewedAt); Assigned(fr08, "Ivan Kovačević", fr08AssignedAt);
        Created(fr09, fr09CreatedAt); Reviewed(fr09, "Građevinski radovi", "Nizak", fr09ReviewedAt); Assigned(fr09, "Tomislav Babić", fr09AssignedAt);

        // U radu
        Created(fr10, fr10CreatedAt); Reviewed(fr10, "Voda", "Visok", fr10ReviewedAt); Assigned(fr10, "Ivan Kovačević", fr10AssignedAt); InterventionStarted(fr10, fr10IntStart);
        Created(fr11, fr11CreatedAt); Reviewed(fr11, "Grijanje", "Srednji", fr11ReviewedAt); Assigned(fr11, "Filip Matić", fr11AssignedAt); InterventionStarted(fr11, fr11IntStart);

        // Riješeno
        Created(fr12, fr12CreatedAt); Reviewed(fr12, "Elektrika", "Kritičan", fr12ReviewedAt); Assigned(fr12, "Domagoj Perković", fr12AssignedAt);
        InterventionStarted(fr12, fr12IntStart); Resolved(fr12, fr12IntEnd);

        Created(fr13, fr13CreatedAt); Reviewed(fr13, "Voda", "Srednji", fr13ReviewedAt); Assigned(fr13, "Josip Radić", fr13AssignedAt);
        InterventionStarted(fr13, fr13IntStart); Resolved(fr13, fr13IntEnd);

        // Zatvoreno
        Created(fr14, fr14CreatedAt); Reviewed(fr14, "Elektrika", "Visok", fr14ReviewedAt); Assigned(fr14, "Ivan Kovačević", fr14AssignedAt);
        InterventionStarted(fr14, fr14IntStart); Resolved(fr14, fr14IntEnd); Closed(fr14, fr14ClosedAt);

        Created(fr15, fr15CreatedAt); Reviewed(fr15, "Grijanje", "Kritičan", fr15ReviewedAt); Assigned(fr15, "Tomislav Babić", fr15AssignedAt);
        // Status prijave se biljezi u povijest SAMO kod prve intervencije na dodjeli (isto pravilo
        // kao InterventionsController.StartIntervention) - druga (uspjesna) intervencija na istoj
        // dodjeli vise ne mijenja "Dodijeljeno -> U radu" jer je prijava vec U radu.
        InterventionStarted(fr15, fr15IntAStart); Resolved(fr15, fr15IntBEnd); Closed(fr15, fr15ClosedAt);

        Created(fr16, fr16CreatedAt); Reviewed(fr16, "Voda", "Nizak", fr16ReviewedAt); Assigned(fr16, "Filip Matić", fr16AssignedAt);
        InterventionStarted(fr16, fr16IntStart); Resolved(fr16, fr16IntEnd); Closed(fr16, fr16ClosedAt);

        Created(fr17, fr17CreatedAt); Reviewed(fr17, "Mreža", "Srednji", fr17ReviewedAt); Assigned(fr17, "Josip Radić", fr17AssignedAt);
        InterventionStarted(fr17, fr17IntStart); Resolved(fr17, fr17IntEnd); Closed(fr17, fr17ClosedAt);

        Created(fr18, fr18CreatedAt); Reviewed(fr18, "Ostalo", "Nizak", fr18ReviewedAt); Assigned(fr18, "Domagoj Perković", fr18AssignedAt);
        InterventionStarted(fr18, fr18IntStart); Resolved(fr18, fr18IntEnd); Closed(fr18, fr18ClosedAt);

        db.FaultReportHistoryEvents.AddRange(history);
        await db.SaveChangesAsync();
    }

    private static FaultReport NewReport(
        int locationId, int reporterId, string description, int? faultTypeId, int? faultPriorityId,
        int faultStatusId, DateTime? dueDate, DateTime createdAt, DateTime updatedAt)
    {
        return new FaultReport
        {
            LocationId = locationId,
            ReporterId = reporterId,
            Title = description.Length > 200 ? description[..200] : description,
            Description = description,
            FaultTypeId = faultTypeId,
            FaultPriorityId = faultPriorityId,
            FaultStatusId = faultStatusId,
            DueDate = dueDate,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    private static WorkAssignment NewAssignment(int faultReportId, int technicianId, DateTime assignedAt, bool isActive, DateTime? unassignedAt, string? reassignmentNote)
    {
        return new WorkAssignment
        {
            FaultReportId = faultReportId,
            TechnicianId = technicianId,
            AssignedAt = assignedAt,
            AssignedByAppUserId = SystemAppUserId,
            UnassignedAt = unassignedAt,
            IsActive = isActive,
            ReassignmentNote = reassignmentNote
        };
    }

    private static Intervention NewIntervention(int workAssignmentId, int interventionStatusId, DateTime? startedAt, DateTime? endedAt, int? durationMinutes, string? notes)
    {
        return new Intervention
        {
            WorkAssignmentId = workAssignmentId,
            InterventionStatusId = interventionStatusId,
            StartedAt = startedAt,
            EndedAt = endedAt,
            DurationMinutes = durationMinutes,
            Notes = notes,
            CreatedAt = startedAt ?? DateTime.UtcNow
        };
    }

    private static int DurationMinutes(DateTime startedAt, DateTime endedAt) => (int)Math.Round((endedAt - startedAt).TotalMinutes);

    private static async Task<(string StoredFileName, long SizeBytes)> SaveSeedImageAsync(string uploadsFolder, byte r, byte g, byte b)
    {
        var bytes = CreatePlaceholderPng(r, g, b);
        var storedFileName = $"{Guid.NewGuid()}.png";
        await File.WriteAllBytesAsync(Path.Combine(uploadsFolder, storedFileName), bytes);
        return (storedFileName, bytes.LongLength);
    }

    // Generira minimalnu ispravnu 1x1 RGB PNG datoteku u runtimeu (bez vanjske slikovne
    // biblioteke poput System.Drawing) - koristi System.IO.Compression.ZLibStream (zlib format
    // s Adler32 checksumom, dostupan od .NET 6) za IDAT kompresiju i rucno izracunat CRC32 za
    // svaki chunk, prema PNG specifikaciji.
    private static byte[] CreatePlaceholderPng(byte r, byte g, byte b)
    {
        var rawScanline = new byte[] { 0, r, g, b }; // filter byte 0 (None) + jedan RGB piksel

        using var idatStream = new MemoryStream();
        using (var zlib = new ZLibStream(idatStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(rawScanline, 0, rawScanline.Length);
        }

        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);

        WriteChunk(png, "IHDR", BuildIhdr());
        WriteChunk(png, "IDAT", idatStream.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());

        return png.ToArray();
    }

    private static byte[] BuildIhdr()
    {
        var ihdr = new byte[13];
        WriteBigEndianInt32(ihdr, 0, 1); // sirina = 1px
        WriteBigEndianInt32(ihdr, 4, 1); // visina = 1px
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 2;  // color type: Truecolor (RGB)
        ihdr[10] = 0; // compression method
        ihdr[11] = 0; // filter method
        ihdr[12] = 0; // interlace method
        return ihdr;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);

        var lengthBytes = new byte[4];
        WriteBigEndianInt32(lengthBytes, 0, data.Length);
        stream.Write(lengthBytes, 0, 4);

        stream.Write(typeBytes, 0, 4);
        stream.Write(data, 0, data.Length);

        var crcInput = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcInput, 0, typeBytes.Length);
        Buffer.BlockCopy(data, 0, crcInput, typeBytes.Length, data.Length);

        var crcBytes = new byte[4];
        WriteBigEndianInt32(crcBytes, 0, unchecked((int)Crc32(crcInput)));
        stream.Write(crcBytes, 0, 4);
    }

    private static void WriteBigEndianInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFFu;
    }
}
