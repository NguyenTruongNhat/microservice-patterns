# Event Sourcing Pattern Flow & Structure

Đây là tài liệu mô tả luồng hoạt động (flow) của pattern **Event Sourcing** trong project `MicroservicePatterns`, tuân thủ theo các quy chuẩn kiến trúc đã đề ra trong `AGENT.md`.

## 1. Overview Diagram

Sơ đồ dưới đây thể hiện quy trình của Event Sourcing: thay vì lưu trạng thái hiện tại (Current State) của một đối tượng, hệ thống sẽ lưu trữ toàn bộ các sự kiện thay đổi (Domain Events) theo thời gian vào **Event Store**. Khi cần, trạng thái hiện tại sẽ được tái tạo (Rebuild/Replay) từ các sự kiện này.

```mermaid
flowchart LR
    %% Định nghĩa Actor
    Client([Client / Frontend])

    %% Dịch vụ chính
    subgraph CoreServices ["Event Sourcing Services"]
        direction TB
        AccountSvc["AccountService<br>(Quản lý Tài khoản)"]
        NotificationSvc["NotificationService<br>(Gửi thông báo)"]
    end

    %% Database / Event Store
    subgraph Storage ["PostgreSQL (Event Store)"]
        direction TB
        EventDB[("EventStreams & Events<br>Tables")]
        PgNotify{{"PostgreSQL<br>LISTEN / NOTIFY"}}
    end

    %% Luồng Write (Commands)
    Client -- "1. POST (Open, Deposit, Withdraw)" --> AccountSvc
    AccountSvc -- "2. Append Events" --> EventDB

    %% Luồng Read (Queries / Replay State)
    Client == "3. GET (Current State)" ==> AccountSvc
    AccountSvc -. "4. Load & Replay Events" .-> EventDB

    %% Xử lý bất đồng bộ (Async Side-effects)
    EventDB -- "5. Kích hoạt Trigger" --> PgNotify
    PgNotify -. "6. Consume Events" .-> NotificationSvc

    %% Styling
    classDef service fill:#f9d0c4,stroke:#e06666,stroke-width:2px,color:#000;
    classDef db fill:#cfe2f3,stroke:#3d85c6,stroke-width:2px,color:#000;
    classDef consumer fill:#d9ead3,stroke:#6aa84f,stroke-width:2px,color:#000;
    classDef core fill:#fff2cc,stroke:#d6b656,stroke-width:2px,color:#000;

    class AccountSvc service;
    class NotificationSvc consumer;
    class EventDB db;
    class PgNotify core;
```

---

## 2. Chi tiết luồng xử lý (Flow Details)

Khác với kiến trúc CRUD truyền thống, pattern này yêu cầu mọi nghiệp vụ (Business logic) đều phải sinh ra sự kiện thay vì thay đổi trực tiếp Entity.

### Bước 1: Khởi tạo và Lưu sự kiện (Write / Commands)
- Người dùng gọi các API `POST` đến `AccountService` (ví dụ: mở tài khoản, nạp tiền, rút tiền).
- `AccountService` nhận Command, khởi tạo Aggregate `Account` và thực thi Business Logic.
- Thay vì UPDATE trực tiếp số dư (Balance) vào database, Aggregate sinh ra các **Domain Events** (`AccountOpenedEvent`, `BalanceChangedEvent`).
- Những sự kiện này được ghi mới (Append-Only) vào **Event Store** (bảng `Events` & `EventStreams` trong PostgreSQL). Dữ liệu này là bất biến (Immutable), không bao giờ bị Update hay Delete.

### Bước 2: Tái tạo trạng thái (Read / Replay State)
- Khi Client gọi API `GET` để xem thông tin tài khoản hiện tại.
- `AccountService` truy vấn **tất cả các Events** thuộc về `AccountId` đó từ Event Store theo thứ tự thời gian.
- Khởi tạo một Aggregate `Account` trống, sau đó tuần tự **Replay (Phát lại)** các events vào Aggregate này (thông qua hàm `Apply()`).
- Sau khi chạy hết stream Event, ta thu được Current State để trả về cho Client.

### Bước 3: Side-effects và Integration (Bất đồng bộ)
- Ngay khi một Event được Insert vào bảng `Events` của PostgreSQL, Database trigger cơ chế `NOTIFY`.
- `NotificationService` đang duy trì connection `LISTEN` vào PostgreSQL sẽ lập tức nhận được thông báo về Event mới.
- Service này đọc thông tin Event và thực hiện các tác vụ bên ngoài như: Gửi Push Notification cho người dùng, gửi Email báo biến động số dư,...

---

## 3. Kiến trúc Event Store

| Thành phần | Vai trò | Mô tả |
| --- | --- | --- |
| **Bảng `EventStreams`** | Quản lý phiên bản Aggregate | Chứa tính toàn vẹn phiên bản (`Version`). Giúp ngăn chặn lỗi đồng thời (Concurrency) bằng Optimistic Concurrency. |
| **Bảng `Events`** | Lưu trữ dữ liệu lịch sử | Ghi nhận từng hành động (`Type`, `Data` định dạng JSONB, `CreatedAtUtc`). |

---

> **Note (AI Rules):** Mọi hành vi cập nhật trạng thái trong EventSourcing bắt buộc phải thông qua `ApplyChange(new Event(...))`. Cấm tuyệt đối việc tạo endpoint hoặc hàm để update Entity state nếu không có Event đi kèm.
