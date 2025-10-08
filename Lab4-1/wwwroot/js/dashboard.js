document.addEventListener("DOMContentLoaded", function () {
    function safeParseJson(data) {
        try {
            return JSON.parse(data);
        } catch (e) {
            return null; // dữ liệu không hợp lệ
        }
    }
    async function sendCommand(baseTopic, command) {
        try {
            const res = await fetch("/api/devices/control", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ topic: baseTopic, command })
            });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const result = await res.json();
            console.log(`[API] Đã gửi lệnh '${command}' cho '${baseTopic}'`, result);
        } catch (err) {
            console.error("[API] Lỗi khi gửi lệnh:", err);
        }
    }
    function addToggleButtonListeners() {
        // LED
        document.querySelectorAll(".toggle-led-btn").forEach(button => {
            button.addEventListener("click", function (event) {
                event.preventDefault();
                const baseTopic = this.dataset.topic;
                if (!baseTopic) return console.error("Thiếu data-topic!");
                sendCommand(baseTopic, "toggle");
            });
        });

        // FAN
        document.querySelectorAll(".toggle-fan-btn").forEach(button => {
            button.addEventListener("click", function (event) {
                event.preventDefault();
                const baseTopic = this.dataset.topic;
                if (!baseTopic) return console.error("Thiếu data-topic!");
                sendCommand(baseTopic, "fan_toggle");
            });
        });
    }
    function addDeleteButtonListeners() {
        document.querySelectorAll(".delete-device-btn").forEach(button => {
            button.addEventListener("click", async function (event) {
                event.preventDefault();
                const topic = this.dataset.topic;
                if (!topic) return;

                if (!confirm(`Bạn có chắc muốn xóa thiết bị với topic "${topic}" không?`))
                    return;

                try {
                    const res = await fetch(`/Dashboard/DeleteDevice?topic=${encodeURIComponent(topic)}`, {
                        method: "POST"
                    });
                    if (res.ok) {
                        alert("Đã xóa thiết bị thành công!");
                        this.closest(".col-lg-4").remove(); // Xóa card khỏi giao diện
                    } else {
                        alert("Lỗi khi xóa thiết bị.");
                    }
                } catch (err) {
                    console.error("[DeleteDevice] Lỗi:", err);
                }
            });
        });
    }
    

    let selectedChartTopic = '';
    const topicSelector = document.getElementById('topic-selector');
    const MAX_DATA_POINTS = 20;
    const ctx = document.getElementById('realtimeChart').getContext('2d');

    devices.forEach(device => {
        topicSelector.add(new Option(device.Name, device.Topic));
    });

    const chart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: [],
            datasets: [
                { label: 'Temperature (°C)', data: [], borderColor: 'rgb(255, 99, 132)', backgroundColor: 'rgba(255, 99, 132, 0.1)', borderWidth: 2, fill: true, tension: 0.4 },
                { label: 'Humidity (%)', data: [], borderColor: 'rgb(54, 162, 235)', backgroundColor: 'rgba(54, 162, 235, 0.1)', borderWidth: 2, fill: true, tension: 0.4 }
            ]
        },
        options: {
            responsive: true,
            scales: {
                y: { beginAtZero: false },
                x: { ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 10 } }
            },
            animation: { duration: 200 }
        }
    });

    function clearChart() {
        chart.data.labels = [];
        chart.data.datasets.forEach(dataset => { dataset.data = []; });
        chart.update();
    }

    function updateChart(temp, humi, timeLabel) {
        const label = timeLabel || new Date().toLocaleTimeString('vi-VN');
        chart.data.labels.push(label);
        chart.data.datasets[0].data.push(temp);
        chart.data.datasets[1].data.push(humi);
        if (chart.data.labels.length > MAX_DATA_POINTS) {
            chart.data.labels.shift();
            chart.data.datasets.forEach(dataset => { dataset.data.shift(); });
        }
        chart.update();
    }

    async function fetchHistoricalData(topic) {
        if (!topic) { clearChart(); return; }
        const encodedTopic = encodeURIComponent(topic);
        const response = await fetch(`/api/devices/telemetry?topic=${encodedTopic}`);
        if (response.ok) {
            const data = await response.json();
            clearChart();
            data.forEach(item => {
                const parsed = safeParseJson(item.payload);
                if (parsed) {
                    if (parsed.temp !== undefined && parsed.humi !== undefined) {
                        updateChart(parsed.temp, parsed.humi, new Date(item.timestamp).toLocaleTimeString('vi-VN'));
                    } else if (parsed.alert) {
                        console.warn("Alert trong lịch sử:", parsed.alert);
                    }
                } else {
                    console.error("Invalid JSON in DB:", item.payload);
                }
            });
        } else { console.error("Failed to fetch historical data."); }
    }

    topicSelector.addEventListener('change', function () {
        selectedChartTopic = this.value;
        fetchHistoricalData(selectedChartTopic);
    });

    function updateCardUI(safeTopicId, data) {
        if (!data) return;
        const tempElement = document.getElementById(`temp-${safeTopicId}`);
        const humiElement = document.getElementById(`humi-${safeTopicId}`);
        const lastSeenElement = document.getElementById(`last-seen-${safeTopicId}`);
        const card = document.getElementById(`card-${safeTopicId}`);
        const ledElement = document.getElementById(`led-${safeTopicId}`);

        if (tempElement && data.temp !== undefined) tempElement.innerText = `${data.temp} °C`;
        if (humiElement && data.humi !== undefined) humiElement.innerText = `${data.humi} %`;
        if (lastSeenElement) lastSeenElement.innerText = `Last seen: ${new Date().toLocaleTimeString('vi-VN')}`;

        if (ledElement && data.led_status !== undefined) {
            if (data.led_status === "ON") {
                ledElement.innerText = "ON";
                ledElement.classList.remove('bg-secondary');
                ledElement.classList.add('bg-success');
            } else {
                ledElement.innerText = "OFF";
                ledElement.classList.remove('bg-success');
                ledElement.classList.add('bg-secondary');
            }
        }
        const fanElement = document.getElementById(`fan-${safeTopicId}`);

        if (fanElement && data.fan_status !== undefined) {
            if (data.fan_status === "ON") {
                fanElement.innerText = "ON";
                fanElement.classList.remove('bg-secondary');
                fanElement.classList.add('bg-success');
            } else {
                fanElement.innerText = "OFF";
                fanElement.classList.remove('bg-success');
                fanElement.classList.add('bg-secondary');
            }
        }


        if (card) {
            // 1. Xử lý CẢNH BÁO NHIỆT ĐỘ CAO (HOT Alert)
            if (data.alert && data.alert === "HOT") {
                // Thêm hiệu ứng cảnh báo đỏ
                card.classList.add("hot-alert");
                // Đảm bảo loại bỏ animation xanh khi đang ở trạng thái HOT
                card.classList.remove('card-update-animation');
            } else {
                // Xóa hiệu ứng cảnh báo đỏ nếu không còn HOT
                card.classList.remove("hot-alert");

                // 2. Xử lý HIỆU ỨNG CẬP NHẬT DỮ LIỆU BÌNH THƯỜNG (Aura xanh)
                card.classList.remove('card-update-animation');
                void card.offsetWidth; // trigger reflow
                card.classList.add('card-update-animation');

                // Xóa animation class sau khi kết thúc
                card.addEventListener('animationend', function handler() {
                    card.classList.remove('card-update-animation');
                    card.removeEventListener('animationend', handler);
                });
            }
        }
    }

    function initializeDashboard() {
        document.querySelectorAll("[id^='card-']").forEach(card => {
            const safeTopicId = card.id.replace("card-", "");
            const msg = card.getAttribute("data-message");
            if (msg && msg.trim() !== "") {
                const parsed = safeParseJson(msg);
                if (parsed) updateCardUI(safeTopicId, parsed);
                else console.error(`Invalid JSON in data-message for ${card.id}:`, msg);
            }
        });
        addDeleteButtonListeners(); // 🆕 Gọi hàm thêm nút xóa
    }

    const connection = new signalR.HubConnectionBuilder().withUrl("/dashboardHub").withAutomaticReconnect().build();

    connection.on("UpdateDeviceStatus", function (deviceTopic, payload) {
        const safeId = deviceTopic.replace(/\//g, '-');
        const statusBadge = document.getElementById(`status-${safeId}`);
        const card = document.getElementById(`card-${safeId}`);

        if (statusBadge && card) {
            const data = safeParseJson(payload);
            if (data) {
                if (data.status === "online") {
                    statusBadge.innerText = "Online";
                    statusBadge.classList.remove("bg-danger");
                    statusBadge.classList.add("bg-success");
                    card.style.opacity = "1";
                } else {
                    statusBadge.innerText = "Offline";
                    statusBadge.classList.remove("bg-success");
                    statusBadge.classList.add("bg-danger");
                    card.style.opacity = "0.6";
                }
            } else {
                console.error("Lỗi parse JSON trạng thái:", payload);
            }
        }
    });

    connection.on("ReceiveMessage", function (topic, message) {
        const parsed = safeParseJson(message);
        if (!parsed) {
            console.error("Invalid JSON from SignalR:", message);
            return;
        }

        if (topic === selectedChartTopic && parsed.temp !== undefined && parsed.humi !== undefined) {
            updateChart(parsed.temp, parsed.humi, null);
        }

        const safeTopicId = topic.replace(/[\/.\s]/g, '-');
        const cardElement = document.getElementById(`card-${safeTopicId}`);
        if (cardElement) {
            cardElement.setAttribute('data-message', message);
            updateCardUI(safeTopicId, parsed);
        }
    });

    async function start() {
        try {
            await connection.start();
            console.log("[SignalR] Connection successful.");
            addToggleButtonListeners(connection);
        } catch (err) {
            console.error("[SignalR] Connection failed: ", err);
            setTimeout(start, 5000);
        }
    }

    initializeDashboard();
    start();
});
document.querySelectorAll(".delete-device-btn").forEach(btn => {
    btn.addEventListener("click", async () => {
        const topic = btn.dataset.topic;
        if (!confirm("Are you sure you want to delete this device?")) return;

        const card = btn.closest(".card");
        card.style.transition = "all 0.4s ease";
        card.style.transform = "scale(0.9)";
        card.style.opacity = "0";

        const response = await fetch("/Dashboard/DeleteDevice", {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: new URLSearchParams({ topic })
        });

        if (response.ok) setTimeout(() => card.remove(), 400);
    });
});

