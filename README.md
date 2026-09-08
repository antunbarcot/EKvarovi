# e-Kvarovi Županije

Aplikacija za prijavu i obradu kvarova u županijskim zgradama i lokacijama (upravne zgrade, škole, zdravstvene ustanove, skladišta). Prijavitelj prijavi kvar sa svoje lokacije, upravitelj ga pregleda i odredi vrstu/prioritet pa dodijeli izvršitelju, a izvršitelj onda odrađuje intervencije, evidentira utrošeni materijal i fotografije prije/poslije, sve do zatvaranja prijave.

## Tehnologije

- .NET 10 (SDK 10.0.400)
- Blazor Web App, Interactive Server render mode
- MudBlazor 9.8.0
- ASP.NET Core Web API
- EF Core 10.0.11 + SQLite
- JWT autentikacija (JwtBearer 10.0.10)
- Swagger, samo u Development okruženju

## Struktura

Solution ima tri projekta:

- `EKvarovi.App` - Blazor frontend. Sve ide preko `HttpClient` + DTO-a, nema direktnog pristupa bazi.
- `EKvarovi.Api` - Web API, EF Core, JWT, upload datoteka, poslovna logika.
- `EKvarovi.Shared` - DTO-i i modeli koje dijele App i Api.

Model baze je prije prve migracije nacrtan u `database-model.dbml` (dbdiagram.io format), u rootu solutiona - drži se usklađen sa stvarnim EF Core modelima.

## Pokretanje

### Preduvjeti

.NET SDK 10, Visual Studio s podrškom za .NET 10 (kod mene 18.9) ili `dotnet` CLI.

### JWT tajni ključ

Prije prvog pokretanja ovo je obavezno - API neće ni upaliti ako `Jwt:Key` nije postavljen, `Program.cs` odmah baci exception. `appsettings.json` namjerno ima prazan `Issuer`/`Audience` i nema `Key` uopće, sve ide kroz user secrets:

```
cd EKvarovi.Api
dotnet user-secrets set "Jwt:Key" "<dug nasumičan string, min 32 znaka>"
dotnet user-secrets set "Jwt:Issuer" "EKvarovi.Api"
dotnet user-secrets set "Jwt:Audience" "EKvarovi.App"
```

### Baza i migracije

Nije potrebno ručno pokretati `Update-Database`. `Program.cs` na startu poziva `dbContext.Database.Migrate()`, pa se sve migracije (`InitialCreate`, `AddSystemAppUserSeed`, `AddFaultReportHistory`) same primjene zajedno sa seed podacima za lookup tablice. Baza je `EKvarovi.Api/EKvarovi.db`, obični SQLite file - ako ga obrišete, sljedeći put kad API krene kreira se ponovo od nule i sve migracije prođu iznova (testirano). Odmah nakon migracija pokreće se i `DemoUserSeeder` koji doda 4 demo korisnika (popis ispod), ako već ne postoje.

Lokacije i zaposlenici (`Location`, `Employee`) nemaju seed podatke - to su namjerno "prava" poslovna tablica koja se puni kroz UI, ne kroz migraciju. Na potpuno praznoj bazi to znači da prvo treba prijaviti se kao Admin i kroz Lokacije/Zaposlenici kreirati barem jednu aktivnu lokaciju i jednog zaposlenika s ulogom Prijavitelj (i jednog s ulogom Izvršitelj), tek onda demo Reporter/Technician računi imaju s čime raditi i prijava kvara ima gdje ići.

### Kako pokrenuti

U Visual Studiju - postaviti *Multiple startup projects* na `EKvarovi.Api` i `EKvarovi.App` (oba na Start) pa pokrenuti. Ovo je već spremljeno u `EKvarovi.slnLaunch.user` u rootu.

Ili preko CLI-a, u dva odvojena terminala iz root foldera:

```
dotnet run --project EKvarovi.Api
dotnet run --project EKvarovi.App
```

Api sluša na `https://localhost:7094` (swagger na `/swagger`), App na `https://localhost:7061`. Pažnja: App ima hardkodiran API URL u `EKvarovi.App/Program.cs` (`https://localhost:7094/`), pa Api mora raditi baš na tom portu da se App uspije spojiti.

## Demo korisnički računi

Tehničar i prijavitelj se pri seedanju automatski povežu s prvim postojećim `Employee` zapisom koji ima `IsTechnician`/`IsReporter`, ako takav zapis već postoji u bazi.

**Admin**
- Email: `admin@ekvarovi.hr`
- Lozinka: `Lozinka123!`

**Manager**
- Email: `manager@ekvarovi.hr`
- Lozinka: `Lozinka123!`

**Technician**
- Email: `tehnicar@ekvarovi.hr`
- Lozinka: `Lozinka123!`

**Reporter**
- Email: `prijavitelj@ekvarovi.hr`
- Lozinka: `Lozinka123!`

## Što je napravljeno

### Obavezni dio

- puna povijest umjesto brisanja podataka: `WorkAssignments` (povijest dodjela, uvijek najviše jedna aktivna po prijavi - filtrirani unique index), `Interventions` (više intervencija po dodjeli, i neuspješne ostaju zabilježene), `InterventionMaterials` (M:N materijal-intervencija s količinom), `Attachments`, `FaultReportHistoryEvents`
- 4 uloge, autorizacija na razini kontrolera (`[Authorize(Roles = ...)]`) plus ownership provjere - ne samo skrivanje gumba u UI-ju
- DTO-i odvojeni od entity modela
- filtriranje, pretraga i sortiranje idu preko query parametara na `/api/fault-reports` i `/api/interventions`, znači server-side, ne lokalno u Blazoru
- lookup podaci se dohvaćaju s `LookupsController`-a
- upload slika/dokumenata - provjera content-typea (jpeg/png/webp za slike, pdf za dokumente), max 10 MB, fizičko ime datoteke je GUID, original naziv/content-type/veličina/vrijeme uploada se čuvaju u bazi, brisanje makne i fajl i zapis iz baze
- JWT login (`/api/auth/login`, `/api/auth/me`)
- `/mine` endpointi koji identitet čitaju isključivo iz JWT-a: `/api/fault-reports/mine` (Reporter), `/api/work-assignments/mine` (Technician)
- cijeli tok statusa: Zaprimljeno → Pregledano → Dodijeljeno → U radu → Riješeno → Zatvoreno, s endpointima za review i close (close radi samo Admin/Manager, i samo ako postoji uspješno završena intervencija)
- upravljanje korisnicima, samo Admin - kreiranje, dodjela uloga, deaktivacija, povezivanje s Employee zapisom

### Bonus dio

- usporedba fotografija prije/poslije na profilu prijave
- vremenska crta (timeline) svih promjena na prijavi
- SLA pokazatelji i trendovi po lokaciji i vrsti kvara, računa ih API a ne frontend

### AI dio

Napravljeno preko `IAiService` sučelja s `MockAiService` implementacijom - radi bez ikakvog API ključa, čista heuristika nad tekstom opisa. Dvije stvari:

- prijedlog vrste i prioriteta kvara iz opisa (`POST /api/ai/fault-report-suggestion`)
- sažetak radnog naloga na temelju svih intervencija i materijala (`GET /api/ai/work-order-summary/{id}`)

AI ovdje ništa sam ne sprema - samo vrati prijedlog, korisnik (Admin/Manager) ga mora ručno potvrditi kroz postojeći review endpoint prije nego se stvarno spremi.

## Sigurnost

JWT ključ ide isključivo kroz `dotnet user-secrets`, nikad u `appsettings.json` ili git. Lozinke su hashirane preko `PasswordHasher<AppUser>`, ne stoje kao čisti tekst. Autorizacija (role + ownership) provjerava se na API-ju na svakom zaštićenom endpointu, ne samo u UI-ju.
