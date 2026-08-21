import 'package:flutter_secure_storage/flutter_secure_storage.dart';

const _androidOptions = AndroidOptions(resetOnError: true);

/// Secure storage configured to recover when Android restores encrypted
/// preferences without the matching keystore key. The plugin clears corrupt
/// values and returns this sentinel; it must never be treated as a token.
const appSecureStorage = FlutterSecureStorage(
  aOptions: _androidOptions,
);

String? normalizeSecureStorageValue(String? value) =>
    value == 'Data has been reset' ? null : value;

extension RecoveringSecureStorage on FlutterSecureStorage {
  Future<String?> readRecovering({required String key}) async {
    final value = await read(key: key, aOptions: _androidOptions);
    return normalizeSecureStorageValue(value);
  }

  Future<void> writeRecovering({
    required String key,
    required String value,
  }) async {
    await write(key: key, value: value, aOptions: _androidOptions);
    // A write can be the first operation to discover corrupt restored data.
    // In that case the Android plugin resets storage instead of completing the
    // write, so verify once and retry against the clean store.
    final persisted = normalizeSecureStorageValue(
      await read(key: key, aOptions: _androidOptions),
    );
    if (persisted != value) {
      await write(key: key, value: value, aOptions: _androidOptions);
    }
  }

  Future<void> deleteRecovering({required String key}) =>
      delete(key: key, aOptions: _androidOptions);
}
