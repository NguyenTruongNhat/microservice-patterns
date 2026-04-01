# CQRS Pattern Flow & Structure

Đây là tài liệu mô tả luồng hoạt động (flow) của pattern **CQRS** trong project `MicroservicePatterns`, tuân thủ theo các quy chuẩn kiến trúc đã đề ra trong `AGENT.md`.

## 1. Overview Diagram

Sơ đồ dưới đây thể hiện sự phân tách giữa **Command** (Write side) và **Query** (Read side), cùng cơ chế đồng bộ dữ liệu thông qua **Integration Events** (EventBus/Kafka).

```mermaid
flowchart LR
    %% Định nghĩa các Actor & Entrypoints
    Client[Client / Frontend]
    
    %% Phần Write (Command Side)
    subgraph WriteSide [Command Side - Write Models]
        direction TB
        BookSvc["BookService<br>(Sách)"]
        BorrowerSvc["BorrowerService<br>(Người mượn)"]
        BorrowingSvc["BorrowingService<br>(Giao dịch mượn)"]
        
        DB_Book[(Book DB)]
        DB_Borrower[(Borrower DB)]
        DB_Borrowing[(Borrowing DB)]
        
        BookSvc -.->|Save| DB_Book
        BorrowerSvc -.->|Save| DB_Borrower
        BorrowingSvc -.->|Save| DB_Borrowing
    end

    %% Event Bus (Kafka)
    EventBus{{EventBus / Kafka}}

    %% Phần Read (Query Side)
    subgraph ReadSide [Query Side - Read Model]
        direction TB
        HistorySvc["BorrowingHistoryService<br>(Lịch sử mượn trả)"]
        DB_History[("Denormalized<br>Read DB")]
        
        HistorySvc -.->|"Save pre-aggregated<br>data"| DB_History
    end

    %% Client Interactions (Write)
    Client -->|"1. POST /books<br>(Create Book)"| BookSvc
    Client -->|"2. POST /borrowers<br>(Create Borrower)"| BorrowerSvc
    Client -->|"3. POST /borrowings<br>(Create Borrowing)"| BorrowingSvc

    %% Message Bus Mapping
    BookSvc ==>|"Publish:<br>BookCreatedEvent"| EventBus
    BorrowerSvc ==>|"Publish:<br>BorrowerCreatedEvent"| EventBus
    BorrowingSvc ==>|"Publish:<br>BorrowingCreatedEvent"| EventBus

    %% Sync data
    EventBus ==>|"Consume<br>Events"| HistorySvc

    %% Client Interactions (Read)
    Client ==>|"4. GET /history/items<br>(Fast Query)"| HistorySvc
    
    %% Styling
    classDef write fill:#f9d0c4,stroke:#e06666,stroke-width:2px;
    classDef read fill:#d9ead3,stroke:#6aa84f,stroke-width:2px;
    classDef db fill:#cfe2f3,stroke:#3d85c6,stroke-width:1px;
    classDef mq fill:#fff2cc,stroke:#d6b656,stroke-width:2px;
    
    class BookSvc,BorrowerSvc,BorrowingSvc write;
    class HistorySvc read;
    class DB_Book,DB_Borrower,DB_Borrowing,DB_History db;
    class EventBus mq;
```

---

## 2. Chi tiết luồng xử lý (Flow Details)

Theo chuẩn CQRS của dự án, mọi thao tác ghi (Write) và đọc (Read) được tách biệt hoàn toàn về mặt Database và Service.

### Bước 1: Khởi tạo dữ liệu (Write / Commands)
- Người dùng gọi API `POST` đến các Write Services (`BookService`, `BorrowerService`, `BorrowingService`).
- Service xử lý Command:
  - Lưu thực thể (Entity) vào Database cục bộ của service đó.
  - Publish một **Integration Event** (ví dụ: `BookCreatedIntegrationEvent`) vào Messaging Broker (Kafka thông qua `EventBus`).
- **Convention:** Write DB được chuẩn hóa cao nhất có thể, tập trung vào Transaction (ACID) của nghiệp vụ cụ thể.

### Bước 2: Đồng bộ dữ liệu sang Read Model
- `BorrowingHistoryService` đóng vai trò là **Read Model**.
- Service này subscribe tất cả các Integration Events từ các Write Services:
  - Nhận `BookCreated...` -> Lưu thông tin sách.
  - Nhận `BorrowerCreated...` -> Lưu thông tin người mượn.
  - Nhận `BorrowingCreated...` -> Nối dữ liệu (Denormalize) giữa Sách, Người mượn và Giao dịch để tạo thành một bảng `History` dạng phẳng (Flat Table).
- **Convention:** Read DB ở đây là "Denormalized", dữ liệu dư thừa được chấp nhận đổi lấy tốc độ truy vấn nhah chóng. Tất cả dữ liệu query cần thiết đều đã được join sẵn khi ghi.

### Bước 3: Truy vấn dữ liệu (Read / Queries)
- Khi Client cần xem lịch sử đầy đủ một giao dịch, Client gọi `GET` đến `BorrowingHistoryService`.
- Data được lấy trực tiếp từ Read DB vô cùng nhanh gọn, **KHÔNG CÓ JOIN, KHÔNG CÓ HTTP CALL CHÉO GIỮA CÁC SERVICES**.

---

## 3. Kiến trúc Database

| Service | Vai trò | Lưu trữ DB (Concept) |
| --- | --- | --- |
| **BookService** | Write | Bảng `Books` (id, title, author) |
| **BorrowerService** | Write | Bảng `Borrowers` (id, name) |
| **BorrowingService** | Write | Bảng `Borrowings` (id, bookId, borrowerId, date) |
| **BorrowingHistoryService** | Read (View) | Bảng `BorrowingHistoryItems` (chứa toàn bộ thông tin Sách, Người, và Ngày mượn đã được JOIN sẵn) |

---

> **Note (AI Rules):** Bất cứ tính năng / field nào mới được thêm vào CQRS flow này đều phải tuân thủ chuẩn: 
> Cập nhật vào Write Service -> Bắn Event -> Cập nhật cấu trúc Denormalized DB bên Read Service. 
> Tuyệt đối không query API của Write Service trong Read Service để lấy dữ liệu.
