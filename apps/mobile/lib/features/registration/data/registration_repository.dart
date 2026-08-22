import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/services/api_client.dart';

final registrationApiClientProvider = Provider<ApiClient>((_) => ApiClient());
final registrationRepositoryProvider = Provider<RegistrationRepository>(
  (ref) => RegistrationRepository(ref.watch(registrationApiClientProvider)),
);
final competitionRegistrationFormsProvider =
    FutureProvider.family<List<MobileRegistrationForm>, String>((ref, id) =>
        ref.watch(registrationRepositoryProvider).formsForCompetition(id));
final myRegistrationsProvider = FutureProvider<List<MobileRegistration>>(
  (ref) => ref.watch(registrationRepositoryProvider).myRegistrations(),
);

class RegistrationRepository {
  RegistrationRepository(this._api);
  final ApiClient _api;

  Future<List<MobileRegistrationForm>> formsForCompetition(String id) async {
    final response = await _api.get<List<dynamic>>(
      '/api/registrationforms/public/competition/$id',
    );
    return (response.data ?? const [])
        .map((item) => MobileRegistrationForm.fromJson(
            Map<String, dynamic>.from(item as Map)))
        .toList(growable: false);
  }

  Future<List<MobileApplicantParticipant>> participants() async {
    final response = await _api.get<List<dynamic>>('/api/applicant/participants');
    return (response.data ?? const [])
        .map((item) => MobileApplicantParticipant.fromJson(
            Map<String, dynamic>.from(item as Map)))
        .toList(growable: false);
  }

  Future<MobileApplicantParticipant> createParticipant({
    required String name,
    required String shortName,
    required String type,
  }) async {
    final response = await _api.post<Map<String, dynamic>>(
      '/api/applicant/participants',
      data: {'name': name, 'shortName': shortName, 'type': type},
    );
    return MobileApplicantParticipant.fromJson(response.data!);
  }

  Future<String> createDraft({
    required String competitionId,
    required String participantId,
    required String formId,
  }) async {
    final response = await _api.post<Map<String, dynamic>>(
      '/api/applicant/registrations',
      data: {
        'competitionId': competitionId,
        'participantId': participantId,
        'formId': formId,
      },
    );
    return response.data!['id'].toString();
  }

  Future<void> saveDraft(String id, Map<String, dynamic> formData) => _api.put(
        '/api/applicant/registrations/$id/draft',
        data: {'formData': formData},
      );

  Future<void> submit(String id) =>
      _api.post('/api/applicant/registrations/$id/submit');

  Future<Map<String, dynamic>> upload({
    required String registrationId,
    required String fieldId,
    required String fileName,
    required Uint8List bytes,
  }) async {
    final response = await _api.post<Map<String, dynamic>>(
      '/api/applicant/registrations/$registrationId/files/$fieldId',
      data: FormData.fromMap({
        'file': MultipartFile.fromBytes(bytes, filename: fileName),
      }),
      options: Options(contentType: 'multipart/form-data'),
    );
    return response.data!;
  }

  Future<List<MobileRegistration>> myRegistrations() async {
    final response = await _api.get<List<dynamic>>('/api/applicant/registrations');
    return (response.data ?? const [])
        .map((item) => MobileRegistration.fromJson(
            Map<String, dynamic>.from(item as Map)))
        .toList(growable: false);
  }

  Future<Map<String, dynamic>> registrationDetail(String id) async {
    final response = await _api.get<Map<String, dynamic>>(
      '/api/applicant/registrations/$id',
    );
    return response.data!;
  }

  Future<void> withdraw(String id, String reason) => _api.post(
        '/api/applicant/registrations/$id/withdraw',
        data: {'reason': reason},
      );
}

class MobileRegistrationForm {
  const MobileRegistrationForm({required this.id, required this.name, required this.version, required this.fields, this.description});
  factory MobileRegistrationForm.fromJson(Map<String, dynamic> json) =>
      MobileRegistrationForm(
        id: json['id'].toString(), name: json['name'].toString(),
        version: (json['version'] as num?)?.toInt() ?? 1,
        description: json['description'] as String?,
        fields: (json['fields'] as List<dynamic>? ?? const [])
            .map((item) => Map<String, dynamic>.from(item as Map))
            .toList(growable: false),
      );
  final String id;
  final String name;
  final int version;
  final String? description;
  final List<Map<String, dynamic>> fields;
}

class MobileApplicantParticipant {
  const MobileApplicantParticipant({required this.id, required this.name, required this.shortName, required this.type});
  factory MobileApplicantParticipant.fromJson(Map<String, dynamic> json) =>
      MobileApplicantParticipant(id: json['id'].toString(), name: json['name'].toString(), shortName: json['shortName'].toString(), type: json['type'].toString());
  final String id;
  final String name;
  final String shortName;
  final String type;
}

class MobileRegistration {
  const MobileRegistration({required this.id, required this.competitionId, required this.competitionName, required this.participantName, required this.status, this.formStatus, this.reviewNotes, this.cancellationReason});
  factory MobileRegistration.fromJson(Map<String, dynamic> json) => MobileRegistration(
    id: json['id'].toString(), competitionId: json['competitionId'].toString(),
    competitionName: json['competitionName']?.toString() ?? 'Competition',
    participantName: json['participantName']?.toString() ?? 'Participant',
    status: json['status'].toString(), formStatus: json['formStatus']?.toString(),
    reviewNotes: json['reviewNotes'] as String?, cancellationReason: json['cancellationReason'] as String?,
  );
  final String id;
  final String competitionId;
  final String competitionName;
  final String participantName;
  final String status;
  final String? formStatus;
  final String? reviewNotes;
  final String? cancellationReason;
}
