# Digital Evidence Chain Management System (DECMS)

## Overview
DECMS is a secure, web-based digital evidence management system designed for law enforcement, forensic laboratories, and judicial departments. It maintains a verifiable chain of custody, guarantees digital evidence integrity via SHA-256 cryptographic hashing, and enforces role-based access control (RBAC).

**The entire database has been fully migrated to MongoDB (NoSQL).**

---

## Technology Stack
- **Backend:** ASP.NET Core 8 MVC, C#
- **Database:** MongoDB (using official `MongoDB.Driver` & `EphemeralMongo` runner)
- **Identity & Security:** ASP.NET Core Identity with custom MongoDB User & Role Stores, Cookie Authentication, PBKDF2 Password Hashing
- **Reporting:** QuestPDF (PDF generation)
- **QR Code Tracking:** QRCoder
- **Frontend:** HTML5, Bootstrap 5, Bootstrap Icons

---

## MongoDB Architecture & Collections
The application uses the `DECMS` database in MongoDB. All entities are stored as BSON documents with ObjectId representations:

1. **`Users`**: Application user credentials, profile information, and roles.
2. **`Roles`**: System roles (`Admin`, `Police Officer`, `Investigator`, `Forensic Analyst`, `Court Officer`).
3. **`Cases`**: Criminal investigation cases with timestamps, priority levels, and assigned officers.
4. **`Evidences`**: Digital evidence records, physical file paths in `SecureUploads`, MIME types, and immutable SHA-256 hashes.
5. **`CustodyRecords`**: Chronological, immutable custody transfers with reason, location, and handling metadata.
6. **`ForensicAnalyses`**: Analysis methodologies, tools used, and forensic findings.
7. **`AuditLogs`**: Complete system-wide audit trail.

---

## MongoDB Connection Configuration
Configured in `appsettings.json`:
```json
"MongoDbSettings": {
  "ConnectionString": "mongodb://localhost:27017",
  "DatabaseName": "DECMS"
}
```
* **Auto-Launch Runner:** If a local MongoDB instance is not detected on port 27017, the application automatically launches an embedded high-performance MongoDB instance in the background. Zero manual installation required!
* **External / Atlas Support:** You can connect to any MongoDB instance or MongoDB Atlas cluster simply by updating `ConnectionString` in `appsettings.json`.

---

## How to Run
1. Open a terminal in the project directory:
   ```bash
   cd "d:\.net project\DECMS"
   ```
2. Run the application:
   ```bash
   dotnet run
   ```
3. Open your browser and navigate to:
   👉 **http://localhost:5190**

---

## Demo Accounts
The MongoDB database is seeded with realistic accounts and demo investigation cases on first run:
- **Admin:** `admin@decms.local`
- **Police Officer:** `officer@decms.local`
- **Investigator:** `investigator@decms.local`
- **Forensic Analyst:** `forensic@decms.local`
- **Court Officer:** `court@decms.local`

**Password for all accounts:** `Demo@123`

---

## Key Features Demonstrated
1. **Case Management:** Create and review criminal investigation cases.
2. **Secure Upload & Hashing:** Upload digital evidence to a secure server folder outside web root with instant SHA-256 hash calculation.
3. **Tamper Detection (Verify Integrity):** Live recalculation and comparison against stored cryptographic hash.
4. **Chain of Custody:** Transfer evidence between custodians with formal reason, timestamp, and immutable audit logs.
5. **Forensic Analysis:** Analysts log methodology and findings without modifying original evidence.
6. **QR Code Identification:** Dynamic QR code generation for evidence labels.
7. **PDF Reporting:** Instant generation of Case and Evidence Chain of Custody reports.
