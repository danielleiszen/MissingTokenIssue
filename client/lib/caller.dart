import 'dart:convert';
import 'dart:io';

import 'package:grpc/service_api.dart';
import 'package:tokenclient/generated/greet.pbgrpc.dart';
import 'package:tokenclient/services.dart';

import 'package:http/http.dart' as http;

class AuthService extends GrpcLaundryServiceBase
    with GrpcLaundryService<AuthClient>, WebGrpcService {
  String? _token;

  String? get token => _token;

  @override
  AuthClient createClient(ClientChannel channel) {
    return AuthClient(channel);
  }

  Future<String?> login() async {
    await client.login(LoginRequest());

    var res = await postData();

    if (res.isNotEmpty) {
      _token = res;

      return "cool";
    } else {
      return null;
    }
  }

  Future<String> postData() async {
    //Uri.https("192.168.1.30:5000", "/api/data")
    //Uri.parse("your url");
    final Uri uri =
        Uri(scheme: 'https', host: 'localhost', port: 443, path: 'token');
    final response = await http.post(
      uri,
      body: {
        "grant_type": "password",
        "client_id": "client",
        "client_secret": "secret",
        "username": "user",
        "password": "1111",
        "scope": "openid profile email custom roles",
      },
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
      },
      encoding: Encoding.getByName('utf-8'),
    );
    var ret = jsonDecode(response.body);

    return ret['access_token'];
  }
}

class CallerService extends GrpcLaundryServiceBase
    with GrpcLaundryService<GreeterClient>, WebGrpcService {
  final AuthService auth = AuthService();

  @override
  GreeterClient createClient(ClientChannel channel) => GreeterClient(channel);

  Future<String> hello() async {
    String? name;

    if (auth.token == null) {
      name = await auth.login();
    }

    if (auth.token != null && name != null) {
      var uri = Uri(scheme: 'https', host: 'localhost', port: 443, path: 'api');
      var response = await http.get(uri, headers: {
        HttpHeaders.contentTypeHeader: "application/json",
        HttpHeaders.authorizationHeader: "Bearer ${auth.token}"
      });

      var res = await client.sayHello(HelloRequest(name: name),
          options: options(auth.token!));

      return res.message;
    } else {
      return 'Invalid login';
    }
  }
}
