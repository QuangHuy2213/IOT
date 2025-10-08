import 'package:flutter_dotenv/flutter_dotenv.dart';

class Env {
  static String get broker => dotenv.env['MQTT_BROKER'] ?? 'broker.emqx.io';
  static int get port => int.parse(dotenv.env['MQTT_PORT'] ?? '8883'); // default TLS
  static String get username => dotenv.env['MQTT_USERNAME'] ?? '';
  static String get password => dotenv.env['MQTT_PASSWORD'] ?? '';
  static String get clientId => dotenv.env['MQTT_CLIENT_ID'] ?? 'flutter_client';

  // 📌 Topic ESP32 publish telemetry (JSON: temp, humi, led_status, fan_status)
  static String get topicTelemetry =>
      dotenv.env['TOPIC_TELEMETRY'] ?? '/iot/device/1/telemetry';

  // 📌 Topic Flutter gửi lệnh điều khiển cho ESP32
  static String get topicControl =>
      dotenv.env['TOPIC_CONTROL'] ?? '/iot/device/1/control';

  // 📌 API load dữ liệu lịch sử telemetry
  static String get historyApiUrl =>
      dotenv.env['HISTORY_API_URL'] ??
          'https://nonmathematic-danette-hydraulic.ngrok-free.dev/api/devices/telemetry?topic=/iot/device/1/telemetry';
}
