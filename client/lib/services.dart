import 'package:flutter/foundation.dart';
import 'package:grpc/grpc_web.dart';
import 'package:grpc/service_api.dart' as $grpc;

abstract class CommonLaundryService {
  CallOptions options(String accessToken) => WebCallOptions(
      metadata: {'Authorization': 'Bearer $accessToken'},
      withCredentials: true);
}

abstract class GrpcLaundryServiceBase extends CommonLaundryService {
  $grpc.ClientChannel _createChannel();
  $grpc.ClientChannel? _channel;

  $grpc.ClientChannel createChannel() {
    _channel ??= _createChannel();

    return _channel!;
  }

  Future clearChannel() async {
    if (_channel != null) {
      await _channel!.terminate();
      _channel = null;
    }
  }
}

mixin GrpcLaundryService<TClient extends $grpc.Client>
    on GrpcLaundryServiceBase {
  TClient get client {
    return createClient(createChannel());
  }

  @protected
  TClient createClient($grpc.ClientChannel channel);
}

mixin WebGrpcService on GrpcLaundryServiceBase {
  @override
  $grpc.ClientChannel _createChannel() => GrpcWebClientChannel.xhr(
        Uri(scheme: 'https', host: 'localhost', port: 5000),
      );
}
