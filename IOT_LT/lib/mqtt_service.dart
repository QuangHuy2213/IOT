import 'dart:async';
import 'dart:convert';
import 'package:mqtt_client/mqtt_client.dart';
import 'package:mqtt_client/mqtt_server_client.dart';
import 'env.dart';

class MqttService {
  late MqttServerClient _client;

  final _dataCtrl = StreamController<Map<String, dynamic>>.broadcast();
  Stream<Map<String, dynamic>> get dataStream => _dataCtrl.stream;

  Future<void> connect() async {
    _client = MqttServerClient.withPort(Env.broker, Env.clientId, Env.port);
    _client.logging(on: true);
    _client.keepAlivePeriod = 30;
    _client.secure = true; // TLS port 8883
    _client.setProtocolV311();

    final connMessage = MqttConnectMessage()
        .withClientIdentifier(Env.clientId)
        .startClean()
        .withWillQos(MqttQos.atMostOnce);

    _client.connectionMessage = connMessage;

    try {
      await _client.connect(Env.username, Env.password);
      print("✅ Connected to MQTT broker at ${Env.broker}:${Env.port}");
    } catch (e) {
      print("❌ MQTT connection failed: $e");
      _client.disconnect();
      return;
    }

    // Subcribe vào topic telemetry (ESP32 publish JSON)
    _client.subscribe(Env.topicTelemetry, MqttQos.atLeastOnce);

    // Subcribe vào topic control (phản hồi từ ESP32)
    _client.subscribe(Env.topicControl, MqttQos.atLeastOnce);

    // Lắng nghe dữ liệu
    _client.updates?.listen((events) {
      final recMess = events[0].payload as MqttPublishMessage;
      final message =
      MqttPublishPayload.bytesToStringAsString(recMess.payload.message);
      final topic = events[0].topic;

      print("📩 [$topic] $message");

      try {
        final data = jsonDecode(message);
        if (data is Map<String, dynamic>) {
          _dataCtrl.add(data); // bắn ra cho UI
        }
      } catch (e) {
        print("⚠️ Not a JSON: $message");
      }
    });
  }

  // Publish trạng thái LED & FAN
  void publishCommand({required String led, required String fan}) {
    if (_client.connectionStatus?.state == MqttConnectionState.connected) {
      final payload = jsonEncode({
        "led_status": led,
        "fan_status": fan,
      });

      final builder = MqttClientPayloadBuilder();
      builder.addString(payload);

      _client.publishMessage(
        Env.topicControl,
        MqttQos.atLeastOnce,
        builder.payload!,
      );

      print("📤 Sent command: $payload → topic ${Env.topicControl}");
    } else {
      print("⚠️ MQTT not connected, cannot send command");
    }
  }

  // ✅ Gửi JSON tùy ý (vd: {"command":"toggle"})
  void publishCommandRaw(Map<String, dynamic> cmd) {
    if (_client.connectionStatus?.state == MqttConnectionState.connected) {
      final payload = jsonEncode(cmd);
      final builder = MqttClientPayloadBuilder();
      builder.addString(payload);

      _client.publishMessage(
        Env.topicControl,
        MqttQos.atLeastOnce,
        builder.payload!,
      );

      print("📤 Sent raw command: $payload → topic ${Env.topicControl}");
    } else {
      print("⚠️ MQTT not connected, cannot send raw command");
    }
  }

  void disconnect() {
    _client.disconnect();
    print("🔌 Disconnected from MQTT broker");
  }
}
