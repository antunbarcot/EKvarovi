# e-Kvarovi Županije — pravila projekta

Aplikacija za prijavu i obradu kvarova u županijskim zgradama i lokacijama. Puni naziv teme: **3. e-Kvarovi Županije - prijava i obrada kvarova**.

## Arhitektura solutiona

Tri projekta:
- `EKvarovi.App` — Blazor Web App, MudBlazor UI
- `EKvarovi.Api` — ASP.NET Core Web API, EF Core, autentikacija, autorizacija, poslovna logika, rad s datotekama
- `EKvarovi.Shared` — DTO modeli i zajednički tipovi

**KRITIČNO pravilo:** `EKvarovi.App` NIKAD ne smije direktno koristiti `DbContext` niti pristupati SQLite bazi. Sva komunikacija ide preko `HttpClient` + DTO modeli. Entity modeli baze se NIKAD ne koriste izravno kao request/response modeli API-ja — uvijek postoji zaseban DTO sloj.

## Obavezne tehnologije

- SQLite + EF Core (DbContext, DbSet, relacije, migracije)
- Seed podaci za lookup tablice i demo scenarije
- DTO modeli odvojeni za prikaz i za spremanje (npr. `FaultReportDto` vs `SaveFaultReportDto`)
- Swagger za razvoj
- MudBlazor (layout, navigacija, tablice, forme, dijalozi, dashboard)
- JWT autentikacija
- Password hashing (npr. `PasswordHasher<AppUser>`) — lozinke se NIKAD ne spremaju kao čisti tekst
- `AppUser`, `AppRole`, spojni `AppUserRole` — jedan račun može imati više uloga
- Autorizacija prema ulogama I ownership pravilima (ne samo role-based)

## Sigurnost tajni

JWT signing key i druge tajne vrijednosti NIKAD u Git repozitoriju niti hardkodirane u Blazoru. Koristiti `dotnet user-secrets` ili konfiguraciju izvan repozitorija.

## Domenski model (iz specifikacije)

Poslovne cjeline: županijske lokacije, zaposlenici (prijavitelji i izvršitelji), prijave kvarova, dodjela prijave izvršitelju, **povijest ponovnih dodjela**, jedna ili više intervencija po prijavi, materijali i količine po intervenciji, fotografije/dokumenti prije i nakon rada, korisnički računi s više uloga.

**Ne smije se svesti na jedan `TechnicianId` i jednu bilješku na prijavi.** Treba:
- `WorkAssignments` — povijest dodjela; u jednom trenutku najviše jedna aktivna dodjela po prijavi, prethodne dodjele ostaju sačuvane
- `Interventions` — jedna dodjela može imati više intervencija (uključujući neuspješne, koje ostaju u povijesti bez brisanja)
- `InterventionMaterials` — materijal i količina po intervenciji (M:N veza materijal↔intervencija s količinom)
- `Attachments` — fotografija prije rada, fotografija nakon rada, dokument — s jasnom namjenom

### Lookup tablice i seed
- `LocationTypes`: Upravna zgrada, Škola, Zdravstvena ustanova, Skladište
- `FaultTypes`: Elektrika, Voda, Grijanje, Mreža, Građevinski radovi, Ostalo
- `FaultPriorities`: Nizak, Srednji, Visok, Kritičan
- `FaultStatuses`: Zaprimljeno, Pregledano, Dodijeljeno, U radu, Riješeno, Zatvoreno
- `InterventionStatuses`: Planirana, U tijeku, Završena, Neuspješna
- `MaterialUnits`: Komad, Metar, Litra, Kilogram, Paket

### Tijek statusa prijave
Zaprimljeno → Pregledano (nakon pregleda upravitelja) → Dodijeljeno (dodjelom izvršitelju) → U radu (pokretanjem intervencije) → Riješeno (uspješan završetak intervencije) → Zatvoreno (samo nakon završne provjere upravitelja, i samo ako postoji uspješno završena intervencija).

## Uloge

- **Admin** — puni pristup, korisnici, lokacije, sve prijave/naloge/intervencije/materijali, lookup podaci, dashboard
- **Manager** (upravitelj) — pregled svih prijava, filtriranje, određuje vrstu/prioritet/rok, dodjeljuje/ponovno dodjeljuje izvršitelja (bez brisanja povijesti), zatvara prijavu tek nakon uspješne intervencije
- **Technician** (izvršitelj) — vidi samo svoje dodijeljene naloge, pokreće intervencije, evidentira rad/materijal/fotografije, ne smije mijenjati prioritet niti dodjeljivati drugima
- **Reporter** (prijavitelj) — unosi prijavu za svoju lokaciju, vidi samo svoje prijave, ne smije dodjeljivati/mijenjati prioritet/zatvarati

## Minimalni tehnički kriteriji (moraju biti ispunjeni)

1. Barem jedan puni CRUD: GET lista, GET by id, POST, PUT, poslovno opravdan DELETE. Entiteti s poviješću (npr. prijave s intervencijama) se ne brišu fizički — arhiviranje/status umjesto brisanja.
2. API vraća ispravne HTTP kodove: 200, 201, 204, 400, 401, 403, 404 gdje je primjenjivo.
3. Barem dva glavna tablična prikaza: tekstualna pretraga + barem 2 kombinirana filtera + sortiranje + reset filtera.
4. Barem jedan od tih popisa šalje filtere/sortiranje API-ju preko query parametara (ne samo lokalno filtriranje u Blazoru).
5. Lookup vrijednosti dolaze s API endpointa; UI prikazuje naziv, DTO/baza sprema ID.
6. Dashboard agregate računa API iz baze (ne dohvaća cijelu bazu da frontend računa lokalno).
7. Upload: provjera dopuštenih tipova i max veličine; sigurno generirano fizičko ime; baza čuva original naziv, spremljeni naziv/putanju, content type, veličinu, vrijeme uploada. Brisanje uklanja i fizičku datoteku i DB zapis.
8. Jedan račun može imati više uloga; Admin kreira/deaktivira korisnike, mijenja uloge, povezuje račun s prijaviteljem/izvršiteljem.
9. Skrivanje gumba u Blazoru NIJE autorizacija — svaki zaštićeni endpoint mora provoditi autorizaciju na API strani.
10. Barem jedan `/mine` endpoint čita identitet isključivo iz JWT claima (npr. `GET /api/faultreports/mine`, `GET /api/workassignments/mine`) — klijent ne smije moći poslati proizvoljan ID da dođe do tuđih podataka.
11. Projekt se builda bez grešaka i pokreće iz prazne baze primjenom svih migracija.
12. App i Api se pokreću zajedno; README ima upute za pokretanje, migracije, demo račune.

## Obavezni tok kroz aplikaciju

```
Blazor stranica → DTO → HttpClient → API Controller → poslovna pravila → DbContext/EF Core → SQLite
```

Zaštićene akcije dodatno prolaze:

```
Login → JWT → role/profile claim → [Authorize]/policy → ownership → dopušteni poslovni podaci
```

Poslovna pravila iz specifikacije moraju biti STVARNO implementirana u API-ju, ne samo dokumentirana ili skrivena u UI-ju.

## Pravila koja API mora provoditi

- Prijava mora pripadati aktivnoj lokaciji
- Kritična prijava mora imati rok
- Prijava u jednom trenutku ne smije imati više od jedne aktivne dodjele
- Izvršitelj smije mijenjati samo svoj aktivni nalog
- Završena intervencija mora imati početak, kraj i bilješku
- Količina materijala mora biti veća od nule
- Uspješan završetak intervencije → status "Riješeno"; "Zatvoreno" postavlja samo Manager nakon provjere
- Neuspješna intervencija ostaje u povijesti i dopušta novu intervenciju na istoj dodjeli
- Ponovna dodjela izvršitelja ne smije prebrisati/izgubiti prethodnu dodjelu
- Prijava s intervencijama se ne briše fizički
- Anonimni/neovlašteni pozivi vraćaju 401 ili 403

## Git

Više smislenih commitova kroz razvoj (ne jedan veliki commit na kraju).

## UI stanja

Svaka važnija stranica: loading, empty, error, validation stanja. Potvrda prije destruktivnih akcija (npr. brisanje).

## DBML

Prije prve migracije, model mora biti nacrtan u DBML formatu (dbdiagram.io) i konzistentan sa stvarnom implementacijom. Ne mijenjati model bez ažuriranja DBML-a — konačni DBML mora odgovarati EF Core modelima pri predaji.
