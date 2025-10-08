import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_dotenv/flutter_dotenv.dart';
import 'mqtt_service.dart';
import 'package:fl_chart/fl_chart.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:http/http.dart' as http;
import 'env.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await dotenv.load(fileName: ".env");
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'IoT Dashboard',
      theme: ThemeData(primarySwatch: Colors.blue),
      home: const HomePage(),
    );
  }
}

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  final mqtt = MqttService();
  final tempData = <FlSpot>[];
  final humiData = <FlSpot>[];
  int counter = 0;

  final notifications = FlutterLocalNotificationsPlugin();

  bool ledOn = false;
  bool fanOn = false;
  double temp = 0;
  double humi = 0;

  @override
  void initState() {
    super.initState();
    initNotifications();

    // 1) load lịch sử trước khi connect MQTT
    loadHistory().then((_) {
      mqtt.connect();

      mqtt.dataStream.listen((data) {
        setState(() {
          temp = (data["temp"] ?? 0).toDouble();
          humi = (data["humi"] ?? 0).toDouble();
          ledOn = data["led_status"] == "ON";
          fanOn = data["fan_status"] == "ON";

          tempData.add(FlSpot(counter.toDouble(), temp));
          humiData.add(FlSpot(counter.toDouble(), humi));
          if (tempData.length > 20) tempData.removeAt(0);
          if (humiData.length > 20) humiData.removeAt(0);
          counter++;
        });

        if (temp > 35) {
          showAlert("⚠️ Cảnh báo nhiệt độ", "Nhiệt độ vượt ngưỡng: $temp °C");
        }
        if (humi > 80) {
          showAlert("⚠️ Cảnh báo độ ẩm", "Độ ẩm vượt ngưỡng: $humi %");
        }
      });
    });
  }

  // 🔽 Load dữ liệu lịch sử từ API
  Future<void> loadHistory() async {
    try {
      final url = Env.historyApiUrl;
      final res = await http.get(Uri.parse(url));
      if (res.statusCode == 200) {
        final List list = jsonDecode(res.body);

        final Iterable items =
        list.length > 20 ? list.sublist(list.length - 20) : list;

        for (final item in items) {
          final payload = jsonDecode(item["payload"]);
          final t = (payload["temp"] ?? 0).toDouble();
          final h = (payload["humi"] ?? 0).toDouble();

          tempData.add(FlSpot(counter.toDouble(), t));
          humiData.add(FlSpot(counter.toDouble(), h));
          counter++;
        }

        if (!mounted) return;
        setState(() {
          if (list.isNotEmpty) {
            final last = jsonDecode(list.last["payload"]);
            temp = (last["temp"] ?? 0).toDouble();
            humi = (last["humi"] ?? 0).toDouble();
            ledOn = last["led_status"] == "ON";
            fanOn = last["fan_status"] == "ON";
          }
        });
      } else {
        print("❌ Load history failed: HTTP ${res.statusCode}");
      }
    } catch (e) {
      print("❌ Load history error: $e");
    }
  }

  void initNotifications() async {
    const settings = InitializationSettings(
      android: AndroidInitializationSettings('@mipmap/ic_launcher'),
    );
    await notifications.initialize(settings);

    // Android 13+ cần xin quyền
    final androidImpl = notifications
        .resolvePlatformSpecificImplementation<
        AndroidFlutterLocalNotificationsPlugin>();

  }

  void showAlert(String title, String body) {
    notifications.show(
      0,
      title,
      body,
      const NotificationDetails(
        android: AndroidNotificationDetails(
          "iot_alerts",
          "IoT Alerts",
          importance: Importance.max,
          priority: Priority.high,
        ),
      ),
    );
  }

  Widget buildChart(List<FlSpot> data, String label, Color color) {
    return SizedBox(
      height: 200,
      child: LineChart(
        LineChartData(
          lineBarsData: [
            LineChartBarData(
              spots: data,
              isCurved: true,
              color: color,
              barWidth: 3,
              belowBarData: BarAreaData(show: false),
            )
          ],
          titlesData: FlTitlesData(show: false),
          borderData: FlBorderData(show: false),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text("IoT Dashboard")),
      body: SingleChildScrollView(
        child: Column(
          children: [
            const SizedBox(height: 16),
            Text("📊 Dữ liệu cảm biến",
                style: Theme.of(context).textTheme.titleLarge),

            buildChart(tempData, "Temperature", Colors.red),
            _card("Temperature (°C)", temp.toStringAsFixed(2)),

            buildChart(humiData, "Humidity", Colors.blue),
            _card("Humidity (%)", humi.toStringAsFixed(2)),

            const Divider(),
            Text("💡 Điều khiển thiết bị",
                style: Theme.of(context).textTheme.titleLarge),

            // ⚡ Toggle LED
            SwitchListTile(
              title: const Text("Bóng đèn"),
              secondary: const Icon(Icons.lightbulb_outline, color: Colors.amber),
              value: ledOn,
              onChanged: (val) {
                setState(() => ledOn = val);
                mqtt.publishCommandRaw({"command": "toggle"});
              },
            ),

            // ⚡ Toggle Fan
            SwitchListTile(
              title: const Text("Quạt"),
              secondary: const Icon(Icons.toys, color: Colors.blue),
              value: fanOn,
              onChanged: (val) {
                setState(() => fanOn = val);
                mqtt.publishCommandRaw({"command": "fan_toggle"});
              },
            ),
          ],
        ),
      ),
    );
  }

  Widget _card(String title, String value) {
    return Card(
      child: ListTile(
        title: Text(title),
        trailing: Text(
          value,
          style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
        ),
      ),
    );
  }
}
