#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <PubSubClient.h>
#include <DHT.h>
#include <ArduinoJson.h>

// ==== Cấu hình WiFi & MQTT ====
const char* WIFI_SSID      = "TDMU";    
const char* WIFI_PASSWORD  = "";          

const char* MQTT_BROKER    = "c0508f574d304264923e98d164312ade.s1.eu.hivemq.cloud";      
const int   MQTT_PORT      = 8883;  // Port SSL HiveMQ Cloud
const char* MQTT_USER      = "Huy123";
const char* MQTT_PASSWORD  = "Quanghuy@123";

// ==== Cấu hình Topics ====
const char* MQTT_DATA_TOPIC    = "iot/device/68e36b755333fe73e1e72964/telemetry";         
const char* MQTT_CONTROL_TOPIC = "iot/device/68e36b755333fe73e1e72964/telemetry/control";
const char* MQTT_STATUS_TOPIC  = "iot/device/68e36b755333fe73e1e72964/status";            

// ==== Cấu hình thiết bị ====
#define DHT_PIN  10       
#define DHT_TYPE DHT22
DHT dht(DHT_PIN, DHT_TYPE);

const int LED_PIN = 21;   // LED
bool ledState = LOW;      

// ==== Cấu hình Quạt với L298N ====
const int FAN_EN  = 5;  
const int FAN_IN1 = 6;  
const int FAN_IN2 = 7;  
bool fanState = false;

// ==== WiFi & MQTT Client ====
WiFiClientSecure espClient;  // SSL
PubSubClient client(espClient);

unsigned long lastMsg = 0;
const long interval = 15000; // Gửi dữ liệu mỗi 15 giây

// Forward declaration
void publishStatus();
void fanControl(bool state);

// ====== Hàm xử lý lệnh MQTT ======
void callback(char* topic, byte* payload, unsigned int length) {
  Serial.print("Nhận được tin nhắn từ topic: ");
  Serial.println(topic);

  if (strcmp(topic, MQTT_CONTROL_TOPIC) == 0) {
    StaticJsonDocument<200> doc;
    DeserializationError error = deserializeJson(doc, payload, length);

    if (error) {
      Serial.print("Lỗi parse JSON: ");
      Serial.println(error.c_str());
      return;
    }

    const char* command = doc["command"]; 

    if (command) {
      if (strcmp(command, "toggle") == 0) {
        ledState = !ledState; 
        digitalWrite(LED_PIN, ledState);
        Serial.println("✅ Đã thay đổi trạng thái LED");
      }
      else if (strcmp(command, "fan_on") == 0) {
        fanControl(true);
        Serial.println("✅ Quạt bật");
      }
      else if (strcmp(command, "fan_off") == 0) {
        fanControl(false);
        Serial.println("✅ Quạt tắt");
      }
      else if (strcmp(command, "fan_toggle") == 0) {
        fanControl(!fanState); 
        if (fanState) {
            Serial.println("✅ Quạt bật (toggled)");
        } else {
            Serial.println("✅ Quạt tắt (toggled)");
        }
      }
    }

    publishStatus(); // Cập nhật trạng thái mới
  }
}

// ====== Hàm điều khiển quạt ======
void fanControl(bool state) {
  fanState = state;
  if (fanState) {
    digitalWrite(FAN_IN1, HIGH);
    digitalWrite(FAN_IN2, LOW);
    analogWrite(FAN_EN, 255);
  } else {
    digitalWrite(FAN_IN1, LOW);
    digitalWrite(FAN_IN2, LOW);
    analogWrite(FAN_EN, 0);
  }
}
// ====== Kết nối WiFi ======
void connectWiFi() {
  Serial.print("🔌 Đang kết nối WiFi: ");
  Serial.println(WIFI_SSID);
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\n✅ Đã kết nối WiFi!");
  Serial.print("📡 IP: ");
  Serial.println(WiFi.localIP());
}

// ====== Kết nối MQTT với SSL + user/pass ======
void connectMQTT() {
  while (!client.connected()) {
    Serial.print("🔄 Đang kết nối MQTT qua SSL...");
    String clientId = "ESP32-DHT-LED-" + String(random(0xffff), HEX);

    const char* offlinePayload = "{\"status\":\"offline\"}";

    // Bỏ qua xác thực certificate (thử nghiệm)
    espClient.setInsecure(); 

    if (client.connect(clientId.c_str(), MQTT_USER, MQTT_PASSWORD, MQTT_STATUS_TOPIC, 1, true, offlinePayload)) {
      Serial.println("✅ Kết nối SSL thành công");

      const char* onlinePayload = "{\"status\":\"online\"}";
      client.publish(MQTT_STATUS_TOPIC, onlinePayload, false);

      client.subscribe(MQTT_CONTROL_TOPIC);
      Serial.print("✅ Đã subscribe topic: ");
      Serial.println(MQTT_CONTROL_TOPIC);

    } else {
      Serial.print("❌ Thất bại, mã lỗi = ");
      Serial.print(client.state());
      Serial.println(" | Thử lại sau 2 giây");
      delay(2000);
    }
  }
}

// ====== Publish trạng thái ======
void publishStatus() {
  float h = dht.readHumidity();
  float t = dht.readTemperature();

  if (isnan(h) || isnan(t)) {
    Serial.println("⚠️ Lỗi đọc cảm biến DHT!");
    return;
  }

  const char* currentLedStateStr = (ledState == HIGH) ? "ON" : "OFF";
  const char* currentFanStateStr = (fanState) ? "ON" : "OFF";

  char payload[256];

  if (t > 60) {
    fanControl(true);
    snprintf(payload, sizeof(payload), 
      "{\"temp\":%.1f,\"humi\":%.1f,\"led_status\":\"%s\",\"fan_status\":\"%s\",\"alert\":\"HOT\"}", 
      t, h, currentLedStateStr, currentFanStateStr);
  } else {
    snprintf(payload, sizeof(payload), 
      "{\"temp\":%.1f,\"humi\":%.1f,\"led_status\":\"%s\",\"fan_status\":\"%s\"}", 
      t, h, currentLedStateStr, currentFanStateStr);
  }

  if (client.publish(MQTT_DATA_TOPIC, payload)) {
    Serial.print("📤 Gửi thành công: ");
    Serial.println(payload);
  } else {
    Serial.println("❌ Gửi MQTT thất bại!");
  }
}

// ====== Setup ======
void setup() {
  Serial.begin(115200);
  
  pinMode(LED_PIN, OUTPUT);
  digitalWrite(LED_PIN, ledState);

  pinMode(FAN_EN, OUTPUT);
  pinMode(FAN_IN1, OUTPUT);
  pinMode(FAN_IN2, OUTPUT);
  fanControl(false);
  
  dht.begin();
  
  connectWiFi();
  
  client.setServer(MQTT_BROKER, MQTT_PORT);
  client.setCallback(callback);
}

// ====== Loop ======
void loop() {
  if (!client.connected()) {
    connectMQTT();
  }
  client.loop();

  unsigned long now = millis();
  if (now - lastMsg > interval) {
    lastMsg = now;
    publishStatus();
  }
}