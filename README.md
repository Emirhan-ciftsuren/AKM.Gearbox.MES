# AKM Gearbox MES - ERP Integration System

This project is an end-to-end integration architecture designed to manage bi-directional data flow and operator tracking processes between an **MES (Manufacturing Execution System)** and **ERP** systems in a manufacturing facility.

---

## 🏗️ Architecture & Technology Stack

* **AKM.Gearbox.MES.API (.NET Web API):** Handles communication with operator UI, updates order statuses, and generates XML outputs for completed orders to be consumed by ERP.
* **AKM.Gearbox.MES.WorkerService (.NET Background Service):** Monitors file system directories (`Input`/`Output`) continuously in the background, parses ERP order XMLs to save into the database, and updates the `IsProcessed` status of finished orders.
* **AKM_Gearbox_MES.WebOperator (ASP.NET Core Razor Pages):** A web-based operator portal where shop-floor operators manage live order statuses (e.g., In Progress, Completed).
* **AKM.Gearbox.MES.Desktop (WPF / WinForms):** Desktop administration and monitoring dashboard.
* **Database:** SQL Server utilizing ADO.NET.

---

## 🔄 Bi-Directional Integration Workflow

1. **ERP ➔ MES (Order Ingestion):** The ERP system drops order XML files into the `Input` directory. The Worker Service reads these files, splits them according to item quantity, and inserts them into the database with a `Pending` status.
2. **Operator Process:** Shop-floor operators view pending orders on the Web Operator interface, transition them to `In Progress`, and mark them as `Completed` upon job completion.
3. **MES ➔ ERP (Feedback Generation):** When an order is completed, the Web API automatically generates a result XML file in the `Output` directory.
4. **Fulfillment & Archiving:** The Worker Service picks up the result XML from the `Output` folder, updates the order's `IsProcessed` flag to `True` (1) in the database, and moves the XML file to the `Archive` directory.

---

## ⚙️ Getting Started & Configuration

To run this project locally, adjust the configurations below according to your local environment:

### 1. Database Setup
Execute the SQL script located in the `Db/` directory on your local SQL Server instance to create the necessary tables and schema.

Update the `DefaultConnection` string in the `appsettings.json` files for the following projects:
* `AKM.Gearbox.MES.API/appsettings.json`
* `AKM.Gearbox.MES.WorkerService/appsettings.json`
* `AKM.Gearbox.MES.Desktop/appconfig.json`
* `AKM.Gearbox.MES.WebOperator/appsettings.json`

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER_NAME;Database=AKM_Gearbox_MES;Trusted_Connection=True;TrustServerCertificate=True;"
}
"FolderSettings": {
    "InputPath": "C:\\MES_Files\\Input",
    "SuccessedPath": "C:\\MES_Files\\Successed",
    "FailedPath": "C:\\MES_Files\\Failed",
    "OutputPath": "C:\\MES_Files\\Output"
  }