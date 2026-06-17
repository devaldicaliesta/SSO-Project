# Cara Menjalankan SSO Master Platform

Panduan menjalankan aplikasi secara lokal. Sudah diverifikasi end-to-end terhadap database **`SSO-PROJECT`** (SQL Server 2022 / SSMS).

## Prasyarat

- **.NET 8 SDK** (proyek mentarget `net8.0`).
- **SQL Server** lokal, default instance `DEVALDICALIESTA` (= `localhost`), Windows Authentication. (Jika Anda memakai instance lain — mis. `(localdb)\MSSQLLocalDB` atau `.\SQLEXPRESS` — ubah `ConnectionStrings:Sso` di `Server/appsettings.Development.json`. **Catatan:** pastikan SSMS terhubung ke server yang sama dengan connection string, jika tidak DB-nya seolah "tidak terbuat".)
- **Sertifikat dev HTTPS** dipercaya (sekali saja):
  ```powershell
  dotnet dev-certs https --trust
  ```

## Konfigurasi (sudah terpasang untuk dev)

`Server/appsettings.Development.json`:
- `ConnectionStrings:Sso` → `Server=DEVALDICALIESTA;Database=SSO-PROJECT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true`
- `Oidc:ClientSecret` → secret BFF (dev). Untuk produksi pindahkan ke `dotnet user-secrets` / environment variable.
- `Admin:Email` / `Admin:Password` → akun admin bootstrap yang di-seed otomatis.

> **Produksi:** jangan simpan secret di appsettings. Gunakan `dotnet user-secrets set "Oidc:ClientSecret" "..."` dan `"ConnectionStrings:Sso" "..."` (UserSecretsId sudah aktif di `Server.csproj`).

## Menjalankan

Database dibuat & di-migrasi & di-seed **otomatis saat startup** — tidak perlu langkah `dotnet ef database update` manual.

```powershell
cd c:\ImDev\MyWorks\SSO\Project-SSO
dotnet run --project Server
```

Buka **https://localhost:5001**.

### Login pertama
1. Klik **Login dengan SSO**.
2. Username: `admin@fundadmin.local` — Password: `Admin#FundAdmin2026!` (sesuai `Admin:Password`).
3. **MFA (TOTP):** scan QR dengan Microsoft Authenticator (atau app TOTP lain) → masukkan kode 6 digit. Secret di-generate per-user dan disimpan terenkripsi.
4. **Ganti password (wajib):** karena admin di-seed dengan `MustChangePassword = true`, setelah MFA Anda diarahkan ke halaman **Ganti Password**. Masukkan password baru (min 12 karakter, kombinasi, dan **tidak boleh sama** dengan password seed). Setelah itu sesi dibuat.
5. Masuk ke Dashboard. Menu kiri dirender dari `GET /api/menu` sesuai permission Anda (admin melihat semua, termasuk **Administration**).

> **Bypass darurat:** jika alur ganti-password bermasalah, set `MustChangePassword = 0` pada baris admin di tabel `AspNetUsers` lewat SSMS, lalu login ulang.

### Admin console (peran Administrator)
- **Administration → Users** (`/admin/users`): buat user (dapat password sementara sekali tampil), enable/disable, unlock, reset password, atur peran.
- **Administration → Roles & Permissions** (`/admin/roles`): buat peran, centang permission per peran (RBAC).
- **Administration → Activity Log** (`/admin/audit`): jejak aktivitas (login, perubahan user/role, perubahan data) — append-only.

> Mengubah peran/permission user akan me-reset *security stamp* sehingga token user tersebut disegarkan dengan permission baru.

## Verifikasi cepat (opsional, lewat SSMS / sqlcmd)

```sql
USE [SSO-PROJECT];
SELECT COUNT(*) FROM Permissions;     -- 9
SELECT COUNT(*) FROM AspNetRoles;     -- 2  (Administrator, InvestmentManager)
SELECT COUNT(*) FROM RolePermissions; -- 13
SELECT COUNT(*) FROM Menus;           -- 10
SELECT TOP 20 OccurredAtUtc, Category, Action, Outcome, ActorUserName FROM AuditEvents ORDER BY Id DESC;
```

## Membuat migration baru (jika mengubah model)

```powershell
$env:SSO_CONNECTION = "Server=DEVALDICALIESTA;Database=SSO-PROJECT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet ef migrations add <Nama> --project SSO.Infrastructure --startup-project SSO.Infrastructure
```
Migration baru otomatis diterapkan saat aplikasi start berikutnya.

## Catatan produksi (dari hasil run)

- **Lisensi Duende** — IdentityServer & BFF menampilkan peringatan lisensi: gratis untuk dev/test, **berbayar untuk produksi**. (Lihat trade-off di `ARCHITECTURE-SSO-Master-v1.md`.)
- **Data Protection keys** — kini tersimpan di `Server/dp-keys` (tanpa enkripsi at-rest). Untuk produksi escrow ke HSM/Vault dan jangan commit (sudah masuk `.gitignore`).
- **Audit ledger** — tabel `AuditEvents` masih tabel biasa; upgrade ke SQL Server 2022 *append-only ledger* (tamper-evident) adalah langkah hardening P4 — lihat `P0-foundation-and-next-steps.md`.
