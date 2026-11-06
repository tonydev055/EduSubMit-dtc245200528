# MepDemo – 5 MEP trong một chương trình console

Mô phỏng hình dạng của 5 mô hình trao đổi thông điệp bằng các thành phần có sẵn của .NET
(`Task`, `Channel<T>`, `IAsyncEnumerable<T>`), chưa cần broker hay mạng thật.

| # | MEP | Mô phỏng bằng | Công nghệ thật (bài) |
|---|-----|---------------|----------------------|
| 1 | Request–response | `await` + `WaitAsync` (timeout) | REST, GraphQL, gRPC (2, 4, 5) |
| 2 | One-way | `Channel` không chờ kết quả | Log/telemetry, broker |
| 3 | Queue-based | `Channel` + 2 worker cạnh tranh | RabbitMQ + Worker Service (6) |
| 4 | Publish–subscribe | `InMemoryEventBus` | RabbitMQ exchange, Kafka (7) |
| 5 | Streaming / push | `IAsyncEnumerable` | SignalR, WebSocket, SSE (3) |

```bash
dotnet run          # chạy cả 5
dotnet run -- 3     # chỉ chạy MEP số 3
```

Gợi ý quan sát: ở demo 3, mỗi `sub-x` chỉ được MỘT worker chấm; ở demo 4, MỘT sự kiện tới cả 3 dịch vụ.
