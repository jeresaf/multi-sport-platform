// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'database.dart';

// ignore_for_file: type=lint
class $OfflineEventsTable extends OfflineEvents
    with TableInfo<$OfflineEventsTable, OfflineEvent> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $OfflineEventsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
      'id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _typeMeta = const VerificationMeta('type');
  @override
  late final GeneratedColumn<String> type = GeneratedColumn<String>(
      'type', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _endpointMeta =
      const VerificationMeta('endpoint');
  @override
  late final GeneratedColumn<String> endpoint = GeneratedColumn<String>(
      'endpoint', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _methodMeta = const VerificationMeta('method');
  @override
  late final GeneratedColumn<String> method = GeneratedColumn<String>(
      'method', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _dataMeta = const VerificationMeta('data');
  @override
  late final GeneratedColumn<String> data = GeneratedColumn<String>(
      'data', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _createdAtMeta =
      const VerificationMeta('createdAt');
  @override
  late final GeneratedColumn<DateTime> createdAt = GeneratedColumn<DateTime>(
      'created_at', aliasedName, false,
      type: DriftSqlType.dateTime, requiredDuringInsert: true);
  static const VerificationMeta _sequenceNumberMeta =
      const VerificationMeta('sequenceNumber');
  @override
  late final GeneratedColumn<int> sequenceNumber = GeneratedColumn<int>(
      'sequence_number', aliasedName, false,
      type: DriftSqlType.int, requiredDuringInsert: true);
  static const VerificationMeta _statusMeta = const VerificationMeta('status');
  @override
  late final GeneratedColumn<String> status = GeneratedColumn<String>(
      'status', aliasedName, false,
      type: DriftSqlType.string,
      requiredDuringInsert: false,
      defaultValue: const Constant('pending'));
  static const VerificationMeta _errorMessageMeta =
      const VerificationMeta('errorMessage');
  @override
  late final GeneratedColumn<String> errorMessage = GeneratedColumn<String>(
      'error_message', aliasedName, true,
      type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _retryCountMeta =
      const VerificationMeta('retryCount');
  @override
  late final GeneratedColumn<int> retryCount = GeneratedColumn<int>(
      'retry_count', aliasedName, false,
      type: DriftSqlType.int,
      requiredDuringInsert: false,
      defaultValue: const Constant(0));
  @override
  List<GeneratedColumn> get $columns => [
        id,
        type,
        endpoint,
        method,
        data,
        createdAt,
        sequenceNumber,
        status,
        errorMessage,
        retryCount
      ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'offline_events';
  @override
  VerificationContext validateIntegrity(Insertable<OfflineEvent> instance,
      {bool isInserting = false}) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('type')) {
      context.handle(
          _typeMeta, type.isAcceptableOrUnknown(data['type']!, _typeMeta));
    } else if (isInserting) {
      context.missing(_typeMeta);
    }
    if (data.containsKey('endpoint')) {
      context.handle(_endpointMeta,
          endpoint.isAcceptableOrUnknown(data['endpoint']!, _endpointMeta));
    } else if (isInserting) {
      context.missing(_endpointMeta);
    }
    if (data.containsKey('method')) {
      context.handle(_methodMeta,
          method.isAcceptableOrUnknown(data['method']!, _methodMeta));
    } else if (isInserting) {
      context.missing(_methodMeta);
    }
    if (data.containsKey('data')) {
      context.handle(
          _dataMeta, this.data.isAcceptableOrUnknown(data['data']!, _dataMeta));
    } else if (isInserting) {
      context.missing(_dataMeta);
    }
    if (data.containsKey('created_at')) {
      context.handle(_createdAtMeta,
          createdAt.isAcceptableOrUnknown(data['created_at']!, _createdAtMeta));
    } else if (isInserting) {
      context.missing(_createdAtMeta);
    }
    if (data.containsKey('sequence_number')) {
      context.handle(
          _sequenceNumberMeta,
          sequenceNumber.isAcceptableOrUnknown(
              data['sequence_number']!, _sequenceNumberMeta));
    } else if (isInserting) {
      context.missing(_sequenceNumberMeta);
    }
    if (data.containsKey('status')) {
      context.handle(_statusMeta,
          status.isAcceptableOrUnknown(data['status']!, _statusMeta));
    }
    if (data.containsKey('error_message')) {
      context.handle(
          _errorMessageMeta,
          errorMessage.isAcceptableOrUnknown(
              data['error_message']!, _errorMessageMeta));
    }
    if (data.containsKey('retry_count')) {
      context.handle(
          _retryCountMeta,
          retryCount.isAcceptableOrUnknown(
              data['retry_count']!, _retryCountMeta));
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  OfflineEvent map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return OfflineEvent(
      id: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}id'])!,
      type: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}type'])!,
      endpoint: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}endpoint'])!,
      method: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}method'])!,
      data: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}data'])!,
      createdAt: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}created_at'])!,
      sequenceNumber: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}sequence_number'])!,
      status: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}status'])!,
      errorMessage: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}error_message']),
      retryCount: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}retry_count'])!,
    );
  }

  @override
  $OfflineEventsTable createAlias(String alias) {
    return $OfflineEventsTable(attachedDatabase, alias);
  }
}

class OfflineEvent extends DataClass implements Insertable<OfflineEvent> {
  final String id;
  final String type;
  final String endpoint;
  final String method;
  final String data;
  final DateTime createdAt;
  final int sequenceNumber;
  final String status;
  final String? errorMessage;
  final int retryCount;
  const OfflineEvent(
      {required this.id,
      required this.type,
      required this.endpoint,
      required this.method,
      required this.data,
      required this.createdAt,
      required this.sequenceNumber,
      required this.status,
      this.errorMessage,
      required this.retryCount});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['type'] = Variable<String>(type);
    map['endpoint'] = Variable<String>(endpoint);
    map['method'] = Variable<String>(method);
    map['data'] = Variable<String>(data);
    map['created_at'] = Variable<DateTime>(createdAt);
    map['sequence_number'] = Variable<int>(sequenceNumber);
    map['status'] = Variable<String>(status);
    if (!nullToAbsent || errorMessage != null) {
      map['error_message'] = Variable<String>(errorMessage);
    }
    map['retry_count'] = Variable<int>(retryCount);
    return map;
  }

  OfflineEventsCompanion toCompanion(bool nullToAbsent) {
    return OfflineEventsCompanion(
      id: Value(id),
      type: Value(type),
      endpoint: Value(endpoint),
      method: Value(method),
      data: Value(data),
      createdAt: Value(createdAt),
      sequenceNumber: Value(sequenceNumber),
      status: Value(status),
      errorMessage: errorMessage == null && nullToAbsent
          ? const Value.absent()
          : Value(errorMessage),
      retryCount: Value(retryCount),
    );
  }

  factory OfflineEvent.fromJson(Map<String, dynamic> json,
      {ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return OfflineEvent(
      id: serializer.fromJson<String>(json['id']),
      type: serializer.fromJson<String>(json['type']),
      endpoint: serializer.fromJson<String>(json['endpoint']),
      method: serializer.fromJson<String>(json['method']),
      data: serializer.fromJson<String>(json['data']),
      createdAt: serializer.fromJson<DateTime>(json['createdAt']),
      sequenceNumber: serializer.fromJson<int>(json['sequenceNumber']),
      status: serializer.fromJson<String>(json['status']),
      errorMessage: serializer.fromJson<String?>(json['errorMessage']),
      retryCount: serializer.fromJson<int>(json['retryCount']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'type': serializer.toJson<String>(type),
      'endpoint': serializer.toJson<String>(endpoint),
      'method': serializer.toJson<String>(method),
      'data': serializer.toJson<String>(data),
      'createdAt': serializer.toJson<DateTime>(createdAt),
      'sequenceNumber': serializer.toJson<int>(sequenceNumber),
      'status': serializer.toJson<String>(status),
      'errorMessage': serializer.toJson<String?>(errorMessage),
      'retryCount': serializer.toJson<int>(retryCount),
    };
  }

  OfflineEvent copyWith(
          {String? id,
          String? type,
          String? endpoint,
          String? method,
          String? data,
          DateTime? createdAt,
          int? sequenceNumber,
          String? status,
          Value<String?> errorMessage = const Value.absent(),
          int? retryCount}) =>
      OfflineEvent(
        id: id ?? this.id,
        type: type ?? this.type,
        endpoint: endpoint ?? this.endpoint,
        method: method ?? this.method,
        data: data ?? this.data,
        createdAt: createdAt ?? this.createdAt,
        sequenceNumber: sequenceNumber ?? this.sequenceNumber,
        status: status ?? this.status,
        errorMessage:
            errorMessage.present ? errorMessage.value : this.errorMessage,
        retryCount: retryCount ?? this.retryCount,
      );
  OfflineEvent copyWithCompanion(OfflineEventsCompanion data) {
    return OfflineEvent(
      id: data.id.present ? data.id.value : this.id,
      type: data.type.present ? data.type.value : this.type,
      endpoint: data.endpoint.present ? data.endpoint.value : this.endpoint,
      method: data.method.present ? data.method.value : this.method,
      data: data.data.present ? data.data.value : this.data,
      createdAt: data.createdAt.present ? data.createdAt.value : this.createdAt,
      sequenceNumber: data.sequenceNumber.present
          ? data.sequenceNumber.value
          : this.sequenceNumber,
      status: data.status.present ? data.status.value : this.status,
      errorMessage: data.errorMessage.present
          ? data.errorMessage.value
          : this.errorMessage,
      retryCount:
          data.retryCount.present ? data.retryCount.value : this.retryCount,
    );
  }

  @override
  String toString() {
    return (StringBuffer('OfflineEvent(')
          ..write('id: $id, ')
          ..write('type: $type, ')
          ..write('endpoint: $endpoint, ')
          ..write('method: $method, ')
          ..write('data: $data, ')
          ..write('createdAt: $createdAt, ')
          ..write('sequenceNumber: $sequenceNumber, ')
          ..write('status: $status, ')
          ..write('errorMessage: $errorMessage, ')
          ..write('retryCount: $retryCount')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(id, type, endpoint, method, data, createdAt,
      sequenceNumber, status, errorMessage, retryCount);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is OfflineEvent &&
          other.id == this.id &&
          other.type == this.type &&
          other.endpoint == this.endpoint &&
          other.method == this.method &&
          other.data == this.data &&
          other.createdAt == this.createdAt &&
          other.sequenceNumber == this.sequenceNumber &&
          other.status == this.status &&
          other.errorMessage == this.errorMessage &&
          other.retryCount == this.retryCount);
}

class OfflineEventsCompanion extends UpdateCompanion<OfflineEvent> {
  final Value<String> id;
  final Value<String> type;
  final Value<String> endpoint;
  final Value<String> method;
  final Value<String> data;
  final Value<DateTime> createdAt;
  final Value<int> sequenceNumber;
  final Value<String> status;
  final Value<String?> errorMessage;
  final Value<int> retryCount;
  final Value<int> rowid;
  const OfflineEventsCompanion({
    this.id = const Value.absent(),
    this.type = const Value.absent(),
    this.endpoint = const Value.absent(),
    this.method = const Value.absent(),
    this.data = const Value.absent(),
    this.createdAt = const Value.absent(),
    this.sequenceNumber = const Value.absent(),
    this.status = const Value.absent(),
    this.errorMessage = const Value.absent(),
    this.retryCount = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  OfflineEventsCompanion.insert({
    required String id,
    required String type,
    required String endpoint,
    required String method,
    required String data,
    required DateTime createdAt,
    required int sequenceNumber,
    this.status = const Value.absent(),
    this.errorMessage = const Value.absent(),
    this.retryCount = const Value.absent(),
    this.rowid = const Value.absent(),
  })  : id = Value(id),
        type = Value(type),
        endpoint = Value(endpoint),
        method = Value(method),
        data = Value(data),
        createdAt = Value(createdAt),
        sequenceNumber = Value(sequenceNumber);
  static Insertable<OfflineEvent> custom({
    Expression<String>? id,
    Expression<String>? type,
    Expression<String>? endpoint,
    Expression<String>? method,
    Expression<String>? data,
    Expression<DateTime>? createdAt,
    Expression<int>? sequenceNumber,
    Expression<String>? status,
    Expression<String>? errorMessage,
    Expression<int>? retryCount,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (type != null) 'type': type,
      if (endpoint != null) 'endpoint': endpoint,
      if (method != null) 'method': method,
      if (data != null) 'data': data,
      if (createdAt != null) 'created_at': createdAt,
      if (sequenceNumber != null) 'sequence_number': sequenceNumber,
      if (status != null) 'status': status,
      if (errorMessage != null) 'error_message': errorMessage,
      if (retryCount != null) 'retry_count': retryCount,
      if (rowid != null) 'rowid': rowid,
    });
  }

  OfflineEventsCompanion copyWith(
      {Value<String>? id,
      Value<String>? type,
      Value<String>? endpoint,
      Value<String>? method,
      Value<String>? data,
      Value<DateTime>? createdAt,
      Value<int>? sequenceNumber,
      Value<String>? status,
      Value<String?>? errorMessage,
      Value<int>? retryCount,
      Value<int>? rowid}) {
    return OfflineEventsCompanion(
      id: id ?? this.id,
      type: type ?? this.type,
      endpoint: endpoint ?? this.endpoint,
      method: method ?? this.method,
      data: data ?? this.data,
      createdAt: createdAt ?? this.createdAt,
      sequenceNumber: sequenceNumber ?? this.sequenceNumber,
      status: status ?? this.status,
      errorMessage: errorMessage ?? this.errorMessage,
      retryCount: retryCount ?? this.retryCount,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (type.present) {
      map['type'] = Variable<String>(type.value);
    }
    if (endpoint.present) {
      map['endpoint'] = Variable<String>(endpoint.value);
    }
    if (method.present) {
      map['method'] = Variable<String>(method.value);
    }
    if (data.present) {
      map['data'] = Variable<String>(data.value);
    }
    if (createdAt.present) {
      map['created_at'] = Variable<DateTime>(createdAt.value);
    }
    if (sequenceNumber.present) {
      map['sequence_number'] = Variable<int>(sequenceNumber.value);
    }
    if (status.present) {
      map['status'] = Variable<String>(status.value);
    }
    if (errorMessage.present) {
      map['error_message'] = Variable<String>(errorMessage.value);
    }
    if (retryCount.present) {
      map['retry_count'] = Variable<int>(retryCount.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('OfflineEventsCompanion(')
          ..write('id: $id, ')
          ..write('type: $type, ')
          ..write('endpoint: $endpoint, ')
          ..write('method: $method, ')
          ..write('data: $data, ')
          ..write('createdAt: $createdAt, ')
          ..write('sequenceNumber: $sequenceNumber, ')
          ..write('status: $status, ')
          ..write('errorMessage: $errorMessage, ')
          ..write('retryCount: $retryCount, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $CachedContestsTable extends CachedContests
    with TableInfo<$CachedContestsTable, CachedContest> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $CachedContestsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
      'id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _tenantIdMeta =
      const VerificationMeta('tenantId');
  @override
  late final GeneratedColumn<String> tenantId = GeneratedColumn<String>(
      'tenant_id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _competitionIdMeta =
      const VerificationMeta('competitionId');
  @override
  late final GeneratedColumn<String> competitionId = GeneratedColumn<String>(
      'competition_id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _nameMeta = const VerificationMeta('name');
  @override
  late final GeneratedColumn<String> name = GeneratedColumn<String>(
      'name', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _statusMeta = const VerificationMeta('status');
  @override
  late final GeneratedColumn<String> status = GeneratedColumn<String>(
      'status', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _sportCodeMeta =
      const VerificationMeta('sportCode');
  @override
  late final GeneratedColumn<String> sportCode = GeneratedColumn<String>(
      'sport_code', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _scheduledAtMeta =
      const VerificationMeta('scheduledAt');
  @override
  late final GeneratedColumn<String> scheduledAt = GeneratedColumn<String>(
      'scheduled_at', aliasedName, true,
      type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _homeParticipantIdMeta =
      const VerificationMeta('homeParticipantId');
  @override
  late final GeneratedColumn<String> homeParticipantId =
      GeneratedColumn<String>('home_participant_id', aliasedName, true,
          type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _homeParticipantNameMeta =
      const VerificationMeta('homeParticipantName');
  @override
  late final GeneratedColumn<String> homeParticipantName =
      GeneratedColumn<String>('home_participant_name', aliasedName, true,
          type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _awayParticipantIdMeta =
      const VerificationMeta('awayParticipantId');
  @override
  late final GeneratedColumn<String> awayParticipantId =
      GeneratedColumn<String>('away_participant_id', aliasedName, true,
          type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _awayParticipantNameMeta =
      const VerificationMeta('awayParticipantName');
  @override
  late final GeneratedColumn<String> awayParticipantName =
      GeneratedColumn<String>('away_participant_name', aliasedName, true,
          type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _homeScoreMeta =
      const VerificationMeta('homeScore');
  @override
  late final GeneratedColumn<int> homeScore = GeneratedColumn<int>(
      'home_score', aliasedName, true,
      type: DriftSqlType.int, requiredDuringInsert: false);
  static const VerificationMeta _awayScoreMeta =
      const VerificationMeta('awayScore');
  @override
  late final GeneratedColumn<int> awayScore = GeneratedColumn<int>(
      'away_score', aliasedName, true,
      type: DriftSqlType.int, requiredDuringInsert: false);
  static const VerificationMeta _cachedAtMeta =
      const VerificationMeta('cachedAt');
  @override
  late final GeneratedColumn<DateTime> cachedAt = GeneratedColumn<DateTime>(
      'cached_at', aliasedName, false,
      type: DriftSqlType.dateTime, requiredDuringInsert: true);
  @override
  List<GeneratedColumn> get $columns => [
        id,
        tenantId,
        competitionId,
        name,
        status,
        sportCode,
        scheduledAt,
        homeParticipantId,
        homeParticipantName,
        awayParticipantId,
        awayParticipantName,
        homeScore,
        awayScore,
        cachedAt
      ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'cached_contests';
  @override
  VerificationContext validateIntegrity(Insertable<CachedContest> instance,
      {bool isInserting = false}) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('tenant_id')) {
      context.handle(_tenantIdMeta,
          tenantId.isAcceptableOrUnknown(data['tenant_id']!, _tenantIdMeta));
    } else if (isInserting) {
      context.missing(_tenantIdMeta);
    }
    if (data.containsKey('competition_id')) {
      context.handle(
          _competitionIdMeta,
          competitionId.isAcceptableOrUnknown(
              data['competition_id']!, _competitionIdMeta));
    } else if (isInserting) {
      context.missing(_competitionIdMeta);
    }
    if (data.containsKey('name')) {
      context.handle(
          _nameMeta, name.isAcceptableOrUnknown(data['name']!, _nameMeta));
    } else if (isInserting) {
      context.missing(_nameMeta);
    }
    if (data.containsKey('status')) {
      context.handle(_statusMeta,
          status.isAcceptableOrUnknown(data['status']!, _statusMeta));
    } else if (isInserting) {
      context.missing(_statusMeta);
    }
    if (data.containsKey('sport_code')) {
      context.handle(_sportCodeMeta,
          sportCode.isAcceptableOrUnknown(data['sport_code']!, _sportCodeMeta));
    } else if (isInserting) {
      context.missing(_sportCodeMeta);
    }
    if (data.containsKey('scheduled_at')) {
      context.handle(
          _scheduledAtMeta,
          scheduledAt.isAcceptableOrUnknown(
              data['scheduled_at']!, _scheduledAtMeta));
    }
    if (data.containsKey('home_participant_id')) {
      context.handle(
          _homeParticipantIdMeta,
          homeParticipantId.isAcceptableOrUnknown(
              data['home_participant_id']!, _homeParticipantIdMeta));
    }
    if (data.containsKey('home_participant_name')) {
      context.handle(
          _homeParticipantNameMeta,
          homeParticipantName.isAcceptableOrUnknown(
              data['home_participant_name']!, _homeParticipantNameMeta));
    }
    if (data.containsKey('away_participant_id')) {
      context.handle(
          _awayParticipantIdMeta,
          awayParticipantId.isAcceptableOrUnknown(
              data['away_participant_id']!, _awayParticipantIdMeta));
    }
    if (data.containsKey('away_participant_name')) {
      context.handle(
          _awayParticipantNameMeta,
          awayParticipantName.isAcceptableOrUnknown(
              data['away_participant_name']!, _awayParticipantNameMeta));
    }
    if (data.containsKey('home_score')) {
      context.handle(_homeScoreMeta,
          homeScore.isAcceptableOrUnknown(data['home_score']!, _homeScoreMeta));
    }
    if (data.containsKey('away_score')) {
      context.handle(_awayScoreMeta,
          awayScore.isAcceptableOrUnknown(data['away_score']!, _awayScoreMeta));
    }
    if (data.containsKey('cached_at')) {
      context.handle(_cachedAtMeta,
          cachedAt.isAcceptableOrUnknown(data['cached_at']!, _cachedAtMeta));
    } else if (isInserting) {
      context.missing(_cachedAtMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  CachedContest map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return CachedContest(
      id: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}id'])!,
      tenantId: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}tenant_id'])!,
      competitionId: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}competition_id'])!,
      name: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}name'])!,
      status: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}status'])!,
      sportCode: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}sport_code'])!,
      scheduledAt: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}scheduled_at']),
      homeParticipantId: attachedDatabase.typeMapping.read(
          DriftSqlType.string, data['${effectivePrefix}home_participant_id']),
      homeParticipantName: attachedDatabase.typeMapping.read(
          DriftSqlType.string, data['${effectivePrefix}home_participant_name']),
      awayParticipantId: attachedDatabase.typeMapping.read(
          DriftSqlType.string, data['${effectivePrefix}away_participant_id']),
      awayParticipantName: attachedDatabase.typeMapping.read(
          DriftSqlType.string, data['${effectivePrefix}away_participant_name']),
      homeScore: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}home_score']),
      awayScore: attachedDatabase.typeMapping
          .read(DriftSqlType.int, data['${effectivePrefix}away_score']),
      cachedAt: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}cached_at'])!,
    );
  }

  @override
  $CachedContestsTable createAlias(String alias) {
    return $CachedContestsTable(attachedDatabase, alias);
  }
}

class CachedContest extends DataClass implements Insertable<CachedContest> {
  final String id;
  final String tenantId;
  final String competitionId;
  final String name;
  final String status;
  final String sportCode;
  final String? scheduledAt;
  final String? homeParticipantId;
  final String? homeParticipantName;
  final String? awayParticipantId;
  final String? awayParticipantName;
  final int? homeScore;
  final int? awayScore;
  final DateTime cachedAt;
  const CachedContest(
      {required this.id,
      required this.tenantId,
      required this.competitionId,
      required this.name,
      required this.status,
      required this.sportCode,
      this.scheduledAt,
      this.homeParticipantId,
      this.homeParticipantName,
      this.awayParticipantId,
      this.awayParticipantName,
      this.homeScore,
      this.awayScore,
      required this.cachedAt});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['tenant_id'] = Variable<String>(tenantId);
    map['competition_id'] = Variable<String>(competitionId);
    map['name'] = Variable<String>(name);
    map['status'] = Variable<String>(status);
    map['sport_code'] = Variable<String>(sportCode);
    if (!nullToAbsent || scheduledAt != null) {
      map['scheduled_at'] = Variable<String>(scheduledAt);
    }
    if (!nullToAbsent || homeParticipantId != null) {
      map['home_participant_id'] = Variable<String>(homeParticipantId);
    }
    if (!nullToAbsent || homeParticipantName != null) {
      map['home_participant_name'] = Variable<String>(homeParticipantName);
    }
    if (!nullToAbsent || awayParticipantId != null) {
      map['away_participant_id'] = Variable<String>(awayParticipantId);
    }
    if (!nullToAbsent || awayParticipantName != null) {
      map['away_participant_name'] = Variable<String>(awayParticipantName);
    }
    if (!nullToAbsent || homeScore != null) {
      map['home_score'] = Variable<int>(homeScore);
    }
    if (!nullToAbsent || awayScore != null) {
      map['away_score'] = Variable<int>(awayScore);
    }
    map['cached_at'] = Variable<DateTime>(cachedAt);
    return map;
  }

  CachedContestsCompanion toCompanion(bool nullToAbsent) {
    return CachedContestsCompanion(
      id: Value(id),
      tenantId: Value(tenantId),
      competitionId: Value(competitionId),
      name: Value(name),
      status: Value(status),
      sportCode: Value(sportCode),
      scheduledAt: scheduledAt == null && nullToAbsent
          ? const Value.absent()
          : Value(scheduledAt),
      homeParticipantId: homeParticipantId == null && nullToAbsent
          ? const Value.absent()
          : Value(homeParticipantId),
      homeParticipantName: homeParticipantName == null && nullToAbsent
          ? const Value.absent()
          : Value(homeParticipantName),
      awayParticipantId: awayParticipantId == null && nullToAbsent
          ? const Value.absent()
          : Value(awayParticipantId),
      awayParticipantName: awayParticipantName == null && nullToAbsent
          ? const Value.absent()
          : Value(awayParticipantName),
      homeScore: homeScore == null && nullToAbsent
          ? const Value.absent()
          : Value(homeScore),
      awayScore: awayScore == null && nullToAbsent
          ? const Value.absent()
          : Value(awayScore),
      cachedAt: Value(cachedAt),
    );
  }

  factory CachedContest.fromJson(Map<String, dynamic> json,
      {ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return CachedContest(
      id: serializer.fromJson<String>(json['id']),
      tenantId: serializer.fromJson<String>(json['tenantId']),
      competitionId: serializer.fromJson<String>(json['competitionId']),
      name: serializer.fromJson<String>(json['name']),
      status: serializer.fromJson<String>(json['status']),
      sportCode: serializer.fromJson<String>(json['sportCode']),
      scheduledAt: serializer.fromJson<String?>(json['scheduledAt']),
      homeParticipantId:
          serializer.fromJson<String?>(json['homeParticipantId']),
      homeParticipantName:
          serializer.fromJson<String?>(json['homeParticipantName']),
      awayParticipantId:
          serializer.fromJson<String?>(json['awayParticipantId']),
      awayParticipantName:
          serializer.fromJson<String?>(json['awayParticipantName']),
      homeScore: serializer.fromJson<int?>(json['homeScore']),
      awayScore: serializer.fromJson<int?>(json['awayScore']),
      cachedAt: serializer.fromJson<DateTime>(json['cachedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'tenantId': serializer.toJson<String>(tenantId),
      'competitionId': serializer.toJson<String>(competitionId),
      'name': serializer.toJson<String>(name),
      'status': serializer.toJson<String>(status),
      'sportCode': serializer.toJson<String>(sportCode),
      'scheduledAt': serializer.toJson<String?>(scheduledAt),
      'homeParticipantId': serializer.toJson<String?>(homeParticipantId),
      'homeParticipantName': serializer.toJson<String?>(homeParticipantName),
      'awayParticipantId': serializer.toJson<String?>(awayParticipantId),
      'awayParticipantName': serializer.toJson<String?>(awayParticipantName),
      'homeScore': serializer.toJson<int?>(homeScore),
      'awayScore': serializer.toJson<int?>(awayScore),
      'cachedAt': serializer.toJson<DateTime>(cachedAt),
    };
  }

  CachedContest copyWith(
          {String? id,
          String? tenantId,
          String? competitionId,
          String? name,
          String? status,
          String? sportCode,
          Value<String?> scheduledAt = const Value.absent(),
          Value<String?> homeParticipantId = const Value.absent(),
          Value<String?> homeParticipantName = const Value.absent(),
          Value<String?> awayParticipantId = const Value.absent(),
          Value<String?> awayParticipantName = const Value.absent(),
          Value<int?> homeScore = const Value.absent(),
          Value<int?> awayScore = const Value.absent(),
          DateTime? cachedAt}) =>
      CachedContest(
        id: id ?? this.id,
        tenantId: tenantId ?? this.tenantId,
        competitionId: competitionId ?? this.competitionId,
        name: name ?? this.name,
        status: status ?? this.status,
        sportCode: sportCode ?? this.sportCode,
        scheduledAt: scheduledAt.present ? scheduledAt.value : this.scheduledAt,
        homeParticipantId: homeParticipantId.present
            ? homeParticipantId.value
            : this.homeParticipantId,
        homeParticipantName: homeParticipantName.present
            ? homeParticipantName.value
            : this.homeParticipantName,
        awayParticipantId: awayParticipantId.present
            ? awayParticipantId.value
            : this.awayParticipantId,
        awayParticipantName: awayParticipantName.present
            ? awayParticipantName.value
            : this.awayParticipantName,
        homeScore: homeScore.present ? homeScore.value : this.homeScore,
        awayScore: awayScore.present ? awayScore.value : this.awayScore,
        cachedAt: cachedAt ?? this.cachedAt,
      );
  CachedContest copyWithCompanion(CachedContestsCompanion data) {
    return CachedContest(
      id: data.id.present ? data.id.value : this.id,
      tenantId: data.tenantId.present ? data.tenantId.value : this.tenantId,
      competitionId: data.competitionId.present
          ? data.competitionId.value
          : this.competitionId,
      name: data.name.present ? data.name.value : this.name,
      status: data.status.present ? data.status.value : this.status,
      sportCode: data.sportCode.present ? data.sportCode.value : this.sportCode,
      scheduledAt:
          data.scheduledAt.present ? data.scheduledAt.value : this.scheduledAt,
      homeParticipantId: data.homeParticipantId.present
          ? data.homeParticipantId.value
          : this.homeParticipantId,
      homeParticipantName: data.homeParticipantName.present
          ? data.homeParticipantName.value
          : this.homeParticipantName,
      awayParticipantId: data.awayParticipantId.present
          ? data.awayParticipantId.value
          : this.awayParticipantId,
      awayParticipantName: data.awayParticipantName.present
          ? data.awayParticipantName.value
          : this.awayParticipantName,
      homeScore: data.homeScore.present ? data.homeScore.value : this.homeScore,
      awayScore: data.awayScore.present ? data.awayScore.value : this.awayScore,
      cachedAt: data.cachedAt.present ? data.cachedAt.value : this.cachedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('CachedContest(')
          ..write('id: $id, ')
          ..write('tenantId: $tenantId, ')
          ..write('competitionId: $competitionId, ')
          ..write('name: $name, ')
          ..write('status: $status, ')
          ..write('sportCode: $sportCode, ')
          ..write('scheduledAt: $scheduledAt, ')
          ..write('homeParticipantId: $homeParticipantId, ')
          ..write('homeParticipantName: $homeParticipantName, ')
          ..write('awayParticipantId: $awayParticipantId, ')
          ..write('awayParticipantName: $awayParticipantName, ')
          ..write('homeScore: $homeScore, ')
          ..write('awayScore: $awayScore, ')
          ..write('cachedAt: $cachedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
      id,
      tenantId,
      competitionId,
      name,
      status,
      sportCode,
      scheduledAt,
      homeParticipantId,
      homeParticipantName,
      awayParticipantId,
      awayParticipantName,
      homeScore,
      awayScore,
      cachedAt);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CachedContest &&
          other.id == this.id &&
          other.tenantId == this.tenantId &&
          other.competitionId == this.competitionId &&
          other.name == this.name &&
          other.status == this.status &&
          other.sportCode == this.sportCode &&
          other.scheduledAt == this.scheduledAt &&
          other.homeParticipantId == this.homeParticipantId &&
          other.homeParticipantName == this.homeParticipantName &&
          other.awayParticipantId == this.awayParticipantId &&
          other.awayParticipantName == this.awayParticipantName &&
          other.homeScore == this.homeScore &&
          other.awayScore == this.awayScore &&
          other.cachedAt == this.cachedAt);
}

class CachedContestsCompanion extends UpdateCompanion<CachedContest> {
  final Value<String> id;
  final Value<String> tenantId;
  final Value<String> competitionId;
  final Value<String> name;
  final Value<String> status;
  final Value<String> sportCode;
  final Value<String?> scheduledAt;
  final Value<String?> homeParticipantId;
  final Value<String?> homeParticipantName;
  final Value<String?> awayParticipantId;
  final Value<String?> awayParticipantName;
  final Value<int?> homeScore;
  final Value<int?> awayScore;
  final Value<DateTime> cachedAt;
  final Value<int> rowid;
  const CachedContestsCompanion({
    this.id = const Value.absent(),
    this.tenantId = const Value.absent(),
    this.competitionId = const Value.absent(),
    this.name = const Value.absent(),
    this.status = const Value.absent(),
    this.sportCode = const Value.absent(),
    this.scheduledAt = const Value.absent(),
    this.homeParticipantId = const Value.absent(),
    this.homeParticipantName = const Value.absent(),
    this.awayParticipantId = const Value.absent(),
    this.awayParticipantName = const Value.absent(),
    this.homeScore = const Value.absent(),
    this.awayScore = const Value.absent(),
    this.cachedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  CachedContestsCompanion.insert({
    required String id,
    required String tenantId,
    required String competitionId,
    required String name,
    required String status,
    required String sportCode,
    this.scheduledAt = const Value.absent(),
    this.homeParticipantId = const Value.absent(),
    this.homeParticipantName = const Value.absent(),
    this.awayParticipantId = const Value.absent(),
    this.awayParticipantName = const Value.absent(),
    this.homeScore = const Value.absent(),
    this.awayScore = const Value.absent(),
    required DateTime cachedAt,
    this.rowid = const Value.absent(),
  })  : id = Value(id),
        tenantId = Value(tenantId),
        competitionId = Value(competitionId),
        name = Value(name),
        status = Value(status),
        sportCode = Value(sportCode),
        cachedAt = Value(cachedAt);
  static Insertable<CachedContest> custom({
    Expression<String>? id,
    Expression<String>? tenantId,
    Expression<String>? competitionId,
    Expression<String>? name,
    Expression<String>? status,
    Expression<String>? sportCode,
    Expression<String>? scheduledAt,
    Expression<String>? homeParticipantId,
    Expression<String>? homeParticipantName,
    Expression<String>? awayParticipantId,
    Expression<String>? awayParticipantName,
    Expression<int>? homeScore,
    Expression<int>? awayScore,
    Expression<DateTime>? cachedAt,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (tenantId != null) 'tenant_id': tenantId,
      if (competitionId != null) 'competition_id': competitionId,
      if (name != null) 'name': name,
      if (status != null) 'status': status,
      if (sportCode != null) 'sport_code': sportCode,
      if (scheduledAt != null) 'scheduled_at': scheduledAt,
      if (homeParticipantId != null) 'home_participant_id': homeParticipantId,
      if (homeParticipantName != null)
        'home_participant_name': homeParticipantName,
      if (awayParticipantId != null) 'away_participant_id': awayParticipantId,
      if (awayParticipantName != null)
        'away_participant_name': awayParticipantName,
      if (homeScore != null) 'home_score': homeScore,
      if (awayScore != null) 'away_score': awayScore,
      if (cachedAt != null) 'cached_at': cachedAt,
      if (rowid != null) 'rowid': rowid,
    });
  }

  CachedContestsCompanion copyWith(
      {Value<String>? id,
      Value<String>? tenantId,
      Value<String>? competitionId,
      Value<String>? name,
      Value<String>? status,
      Value<String>? sportCode,
      Value<String?>? scheduledAt,
      Value<String?>? homeParticipantId,
      Value<String?>? homeParticipantName,
      Value<String?>? awayParticipantId,
      Value<String?>? awayParticipantName,
      Value<int?>? homeScore,
      Value<int?>? awayScore,
      Value<DateTime>? cachedAt,
      Value<int>? rowid}) {
    return CachedContestsCompanion(
      id: id ?? this.id,
      tenantId: tenantId ?? this.tenantId,
      competitionId: competitionId ?? this.competitionId,
      name: name ?? this.name,
      status: status ?? this.status,
      sportCode: sportCode ?? this.sportCode,
      scheduledAt: scheduledAt ?? this.scheduledAt,
      homeParticipantId: homeParticipantId ?? this.homeParticipantId,
      homeParticipantName: homeParticipantName ?? this.homeParticipantName,
      awayParticipantId: awayParticipantId ?? this.awayParticipantId,
      awayParticipantName: awayParticipantName ?? this.awayParticipantName,
      homeScore: homeScore ?? this.homeScore,
      awayScore: awayScore ?? this.awayScore,
      cachedAt: cachedAt ?? this.cachedAt,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (tenantId.present) {
      map['tenant_id'] = Variable<String>(tenantId.value);
    }
    if (competitionId.present) {
      map['competition_id'] = Variable<String>(competitionId.value);
    }
    if (name.present) {
      map['name'] = Variable<String>(name.value);
    }
    if (status.present) {
      map['status'] = Variable<String>(status.value);
    }
    if (sportCode.present) {
      map['sport_code'] = Variable<String>(sportCode.value);
    }
    if (scheduledAt.present) {
      map['scheduled_at'] = Variable<String>(scheduledAt.value);
    }
    if (homeParticipantId.present) {
      map['home_participant_id'] = Variable<String>(homeParticipantId.value);
    }
    if (homeParticipantName.present) {
      map['home_participant_name'] =
          Variable<String>(homeParticipantName.value);
    }
    if (awayParticipantId.present) {
      map['away_participant_id'] = Variable<String>(awayParticipantId.value);
    }
    if (awayParticipantName.present) {
      map['away_participant_name'] =
          Variable<String>(awayParticipantName.value);
    }
    if (homeScore.present) {
      map['home_score'] = Variable<int>(homeScore.value);
    }
    if (awayScore.present) {
      map['away_score'] = Variable<int>(awayScore.value);
    }
    if (cachedAt.present) {
      map['cached_at'] = Variable<DateTime>(cachedAt.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('CachedContestsCompanion(')
          ..write('id: $id, ')
          ..write('tenantId: $tenantId, ')
          ..write('competitionId: $competitionId, ')
          ..write('name: $name, ')
          ..write('status: $status, ')
          ..write('sportCode: $sportCode, ')
          ..write('scheduledAt: $scheduledAt, ')
          ..write('homeParticipantId: $homeParticipantId, ')
          ..write('homeParticipantName: $homeParticipantName, ')
          ..write('awayParticipantId: $awayParticipantId, ')
          ..write('awayParticipantName: $awayParticipantName, ')
          ..write('homeScore: $homeScore, ')
          ..write('awayScore: $awayScore, ')
          ..write('cachedAt: $cachedAt, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $CachedParticipantsTable extends CachedParticipants
    with TableInfo<$CachedParticipantsTable, CachedParticipant> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $CachedParticipantsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
      'id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _tenantIdMeta =
      const VerificationMeta('tenantId');
  @override
  late final GeneratedColumn<String> tenantId = GeneratedColumn<String>(
      'tenant_id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _nameMeta = const VerificationMeta('name');
  @override
  late final GeneratedColumn<String> name = GeneratedColumn<String>(
      'name', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _typeMeta = const VerificationMeta('type');
  @override
  late final GeneratedColumn<String> type = GeneratedColumn<String>(
      'type', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _sportCodeMeta =
      const VerificationMeta('sportCode');
  @override
  late final GeneratedColumn<String> sportCode = GeneratedColumn<String>(
      'sport_code', aliasedName, true,
      type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _avatarUrlMeta =
      const VerificationMeta('avatarUrl');
  @override
  late final GeneratedColumn<String> avatarUrl = GeneratedColumn<String>(
      'avatar_url', aliasedName, true,
      type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _metadataMeta =
      const VerificationMeta('metadata');
  @override
  late final GeneratedColumn<String> metadata = GeneratedColumn<String>(
      'metadata', aliasedName, true,
      type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _cachedAtMeta =
      const VerificationMeta('cachedAt');
  @override
  late final GeneratedColumn<DateTime> cachedAt = GeneratedColumn<DateTime>(
      'cached_at', aliasedName, false,
      type: DriftSqlType.dateTime, requiredDuringInsert: true);
  @override
  List<GeneratedColumn> get $columns =>
      [id, tenantId, name, type, sportCode, avatarUrl, metadata, cachedAt];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'cached_participants';
  @override
  VerificationContext validateIntegrity(Insertable<CachedParticipant> instance,
      {bool isInserting = false}) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('tenant_id')) {
      context.handle(_tenantIdMeta,
          tenantId.isAcceptableOrUnknown(data['tenant_id']!, _tenantIdMeta));
    } else if (isInserting) {
      context.missing(_tenantIdMeta);
    }
    if (data.containsKey('name')) {
      context.handle(
          _nameMeta, name.isAcceptableOrUnknown(data['name']!, _nameMeta));
    } else if (isInserting) {
      context.missing(_nameMeta);
    }
    if (data.containsKey('type')) {
      context.handle(
          _typeMeta, type.isAcceptableOrUnknown(data['type']!, _typeMeta));
    } else if (isInserting) {
      context.missing(_typeMeta);
    }
    if (data.containsKey('sport_code')) {
      context.handle(_sportCodeMeta,
          sportCode.isAcceptableOrUnknown(data['sport_code']!, _sportCodeMeta));
    }
    if (data.containsKey('avatar_url')) {
      context.handle(_avatarUrlMeta,
          avatarUrl.isAcceptableOrUnknown(data['avatar_url']!, _avatarUrlMeta));
    }
    if (data.containsKey('metadata')) {
      context.handle(_metadataMeta,
          metadata.isAcceptableOrUnknown(data['metadata']!, _metadataMeta));
    }
    if (data.containsKey('cached_at')) {
      context.handle(_cachedAtMeta,
          cachedAt.isAcceptableOrUnknown(data['cached_at']!, _cachedAtMeta));
    } else if (isInserting) {
      context.missing(_cachedAtMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  CachedParticipant map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return CachedParticipant(
      id: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}id'])!,
      tenantId: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}tenant_id'])!,
      name: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}name'])!,
      type: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}type'])!,
      sportCode: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}sport_code']),
      avatarUrl: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}avatar_url']),
      metadata: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}metadata']),
      cachedAt: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}cached_at'])!,
    );
  }

  @override
  $CachedParticipantsTable createAlias(String alias) {
    return $CachedParticipantsTable(attachedDatabase, alias);
  }
}

class CachedParticipant extends DataClass
    implements Insertable<CachedParticipant> {
  final String id;
  final String tenantId;
  final String name;
  final String type;
  final String? sportCode;
  final String? avatarUrl;
  final String? metadata;
  final DateTime cachedAt;
  const CachedParticipant(
      {required this.id,
      required this.tenantId,
      required this.name,
      required this.type,
      this.sportCode,
      this.avatarUrl,
      this.metadata,
      required this.cachedAt});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['tenant_id'] = Variable<String>(tenantId);
    map['name'] = Variable<String>(name);
    map['type'] = Variable<String>(type);
    if (!nullToAbsent || sportCode != null) {
      map['sport_code'] = Variable<String>(sportCode);
    }
    if (!nullToAbsent || avatarUrl != null) {
      map['avatar_url'] = Variable<String>(avatarUrl);
    }
    if (!nullToAbsent || metadata != null) {
      map['metadata'] = Variable<String>(metadata);
    }
    map['cached_at'] = Variable<DateTime>(cachedAt);
    return map;
  }

  CachedParticipantsCompanion toCompanion(bool nullToAbsent) {
    return CachedParticipantsCompanion(
      id: Value(id),
      tenantId: Value(tenantId),
      name: Value(name),
      type: Value(type),
      sportCode: sportCode == null && nullToAbsent
          ? const Value.absent()
          : Value(sportCode),
      avatarUrl: avatarUrl == null && nullToAbsent
          ? const Value.absent()
          : Value(avatarUrl),
      metadata: metadata == null && nullToAbsent
          ? const Value.absent()
          : Value(metadata),
      cachedAt: Value(cachedAt),
    );
  }

  factory CachedParticipant.fromJson(Map<String, dynamic> json,
      {ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return CachedParticipant(
      id: serializer.fromJson<String>(json['id']),
      tenantId: serializer.fromJson<String>(json['tenantId']),
      name: serializer.fromJson<String>(json['name']),
      type: serializer.fromJson<String>(json['type']),
      sportCode: serializer.fromJson<String?>(json['sportCode']),
      avatarUrl: serializer.fromJson<String?>(json['avatarUrl']),
      metadata: serializer.fromJson<String?>(json['metadata']),
      cachedAt: serializer.fromJson<DateTime>(json['cachedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'tenantId': serializer.toJson<String>(tenantId),
      'name': serializer.toJson<String>(name),
      'type': serializer.toJson<String>(type),
      'sportCode': serializer.toJson<String?>(sportCode),
      'avatarUrl': serializer.toJson<String?>(avatarUrl),
      'metadata': serializer.toJson<String?>(metadata),
      'cachedAt': serializer.toJson<DateTime>(cachedAt),
    };
  }

  CachedParticipant copyWith(
          {String? id,
          String? tenantId,
          String? name,
          String? type,
          Value<String?> sportCode = const Value.absent(),
          Value<String?> avatarUrl = const Value.absent(),
          Value<String?> metadata = const Value.absent(),
          DateTime? cachedAt}) =>
      CachedParticipant(
        id: id ?? this.id,
        tenantId: tenantId ?? this.tenantId,
        name: name ?? this.name,
        type: type ?? this.type,
        sportCode: sportCode.present ? sportCode.value : this.sportCode,
        avatarUrl: avatarUrl.present ? avatarUrl.value : this.avatarUrl,
        metadata: metadata.present ? metadata.value : this.metadata,
        cachedAt: cachedAt ?? this.cachedAt,
      );
  CachedParticipant copyWithCompanion(CachedParticipantsCompanion data) {
    return CachedParticipant(
      id: data.id.present ? data.id.value : this.id,
      tenantId: data.tenantId.present ? data.tenantId.value : this.tenantId,
      name: data.name.present ? data.name.value : this.name,
      type: data.type.present ? data.type.value : this.type,
      sportCode: data.sportCode.present ? data.sportCode.value : this.sportCode,
      avatarUrl: data.avatarUrl.present ? data.avatarUrl.value : this.avatarUrl,
      metadata: data.metadata.present ? data.metadata.value : this.metadata,
      cachedAt: data.cachedAt.present ? data.cachedAt.value : this.cachedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('CachedParticipant(')
          ..write('id: $id, ')
          ..write('tenantId: $tenantId, ')
          ..write('name: $name, ')
          ..write('type: $type, ')
          ..write('sportCode: $sportCode, ')
          ..write('avatarUrl: $avatarUrl, ')
          ..write('metadata: $metadata, ')
          ..write('cachedAt: $cachedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
      id, tenantId, name, type, sportCode, avatarUrl, metadata, cachedAt);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CachedParticipant &&
          other.id == this.id &&
          other.tenantId == this.tenantId &&
          other.name == this.name &&
          other.type == this.type &&
          other.sportCode == this.sportCode &&
          other.avatarUrl == this.avatarUrl &&
          other.metadata == this.metadata &&
          other.cachedAt == this.cachedAt);
}

class CachedParticipantsCompanion extends UpdateCompanion<CachedParticipant> {
  final Value<String> id;
  final Value<String> tenantId;
  final Value<String> name;
  final Value<String> type;
  final Value<String?> sportCode;
  final Value<String?> avatarUrl;
  final Value<String?> metadata;
  final Value<DateTime> cachedAt;
  final Value<int> rowid;
  const CachedParticipantsCompanion({
    this.id = const Value.absent(),
    this.tenantId = const Value.absent(),
    this.name = const Value.absent(),
    this.type = const Value.absent(),
    this.sportCode = const Value.absent(),
    this.avatarUrl = const Value.absent(),
    this.metadata = const Value.absent(),
    this.cachedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  CachedParticipantsCompanion.insert({
    required String id,
    required String tenantId,
    required String name,
    required String type,
    this.sportCode = const Value.absent(),
    this.avatarUrl = const Value.absent(),
    this.metadata = const Value.absent(),
    required DateTime cachedAt,
    this.rowid = const Value.absent(),
  })  : id = Value(id),
        tenantId = Value(tenantId),
        name = Value(name),
        type = Value(type),
        cachedAt = Value(cachedAt);
  static Insertable<CachedParticipant> custom({
    Expression<String>? id,
    Expression<String>? tenantId,
    Expression<String>? name,
    Expression<String>? type,
    Expression<String>? sportCode,
    Expression<String>? avatarUrl,
    Expression<String>? metadata,
    Expression<DateTime>? cachedAt,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (tenantId != null) 'tenant_id': tenantId,
      if (name != null) 'name': name,
      if (type != null) 'type': type,
      if (sportCode != null) 'sport_code': sportCode,
      if (avatarUrl != null) 'avatar_url': avatarUrl,
      if (metadata != null) 'metadata': metadata,
      if (cachedAt != null) 'cached_at': cachedAt,
      if (rowid != null) 'rowid': rowid,
    });
  }

  CachedParticipantsCompanion copyWith(
      {Value<String>? id,
      Value<String>? tenantId,
      Value<String>? name,
      Value<String>? type,
      Value<String?>? sportCode,
      Value<String?>? avatarUrl,
      Value<String?>? metadata,
      Value<DateTime>? cachedAt,
      Value<int>? rowid}) {
    return CachedParticipantsCompanion(
      id: id ?? this.id,
      tenantId: tenantId ?? this.tenantId,
      name: name ?? this.name,
      type: type ?? this.type,
      sportCode: sportCode ?? this.sportCode,
      avatarUrl: avatarUrl ?? this.avatarUrl,
      metadata: metadata ?? this.metadata,
      cachedAt: cachedAt ?? this.cachedAt,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (tenantId.present) {
      map['tenant_id'] = Variable<String>(tenantId.value);
    }
    if (name.present) {
      map['name'] = Variable<String>(name.value);
    }
    if (type.present) {
      map['type'] = Variable<String>(type.value);
    }
    if (sportCode.present) {
      map['sport_code'] = Variable<String>(sportCode.value);
    }
    if (avatarUrl.present) {
      map['avatar_url'] = Variable<String>(avatarUrl.value);
    }
    if (metadata.present) {
      map['metadata'] = Variable<String>(metadata.value);
    }
    if (cachedAt.present) {
      map['cached_at'] = Variable<DateTime>(cachedAt.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('CachedParticipantsCompanion(')
          ..write('id: $id, ')
          ..write('tenantId: $tenantId, ')
          ..write('name: $name, ')
          ..write('type: $type, ')
          ..write('sportCode: $sportCode, ')
          ..write('avatarUrl: $avatarUrl, ')
          ..write('metadata: $metadata, ')
          ..write('cachedAt: $cachedAt, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $CachedCompetitionsTable extends CachedCompetitions
    with TableInfo<$CachedCompetitionsTable, CachedCompetition> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $CachedCompetitionsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
      'id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _tenantIdMeta =
      const VerificationMeta('tenantId');
  @override
  late final GeneratedColumn<String> tenantId = GeneratedColumn<String>(
      'tenant_id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _nameMeta = const VerificationMeta('name');
  @override
  late final GeneratedColumn<String> name = GeneratedColumn<String>(
      'name', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _sportCodeMeta =
      const VerificationMeta('sportCode');
  @override
  late final GeneratedColumn<String> sportCode = GeneratedColumn<String>(
      'sport_code', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _statusMeta = const VerificationMeta('status');
  @override
  late final GeneratedColumn<String> status = GeneratedColumn<String>(
      'status', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _formatMeta = const VerificationMeta('format');
  @override
  late final GeneratedColumn<String> format = GeneratedColumn<String>(
      'format', aliasedName, true,
      type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _cachedAtMeta =
      const VerificationMeta('cachedAt');
  @override
  late final GeneratedColumn<DateTime> cachedAt = GeneratedColumn<DateTime>(
      'cached_at', aliasedName, false,
      type: DriftSqlType.dateTime, requiredDuringInsert: true);
  @override
  List<GeneratedColumn> get $columns =>
      [id, tenantId, name, sportCode, status, format, cachedAt];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'cached_competitions';
  @override
  VerificationContext validateIntegrity(Insertable<CachedCompetition> instance,
      {bool isInserting = false}) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('tenant_id')) {
      context.handle(_tenantIdMeta,
          tenantId.isAcceptableOrUnknown(data['tenant_id']!, _tenantIdMeta));
    } else if (isInserting) {
      context.missing(_tenantIdMeta);
    }
    if (data.containsKey('name')) {
      context.handle(
          _nameMeta, name.isAcceptableOrUnknown(data['name']!, _nameMeta));
    } else if (isInserting) {
      context.missing(_nameMeta);
    }
    if (data.containsKey('sport_code')) {
      context.handle(_sportCodeMeta,
          sportCode.isAcceptableOrUnknown(data['sport_code']!, _sportCodeMeta));
    } else if (isInserting) {
      context.missing(_sportCodeMeta);
    }
    if (data.containsKey('status')) {
      context.handle(_statusMeta,
          status.isAcceptableOrUnknown(data['status']!, _statusMeta));
    } else if (isInserting) {
      context.missing(_statusMeta);
    }
    if (data.containsKey('format')) {
      context.handle(_formatMeta,
          format.isAcceptableOrUnknown(data['format']!, _formatMeta));
    }
    if (data.containsKey('cached_at')) {
      context.handle(_cachedAtMeta,
          cachedAt.isAcceptableOrUnknown(data['cached_at']!, _cachedAtMeta));
    } else if (isInserting) {
      context.missing(_cachedAtMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  CachedCompetition map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return CachedCompetition(
      id: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}id'])!,
      tenantId: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}tenant_id'])!,
      name: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}name'])!,
      sportCode: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}sport_code'])!,
      status: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}status'])!,
      format: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}format']),
      cachedAt: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}cached_at'])!,
    );
  }

  @override
  $CachedCompetitionsTable createAlias(String alias) {
    return $CachedCompetitionsTable(attachedDatabase, alias);
  }
}

class CachedCompetition extends DataClass
    implements Insertable<CachedCompetition> {
  final String id;
  final String tenantId;
  final String name;
  final String sportCode;
  final String status;
  final String? format;
  final DateTime cachedAt;
  const CachedCompetition(
      {required this.id,
      required this.tenantId,
      required this.name,
      required this.sportCode,
      required this.status,
      this.format,
      required this.cachedAt});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['tenant_id'] = Variable<String>(tenantId);
    map['name'] = Variable<String>(name);
    map['sport_code'] = Variable<String>(sportCode);
    map['status'] = Variable<String>(status);
    if (!nullToAbsent || format != null) {
      map['format'] = Variable<String>(format);
    }
    map['cached_at'] = Variable<DateTime>(cachedAt);
    return map;
  }

  CachedCompetitionsCompanion toCompanion(bool nullToAbsent) {
    return CachedCompetitionsCompanion(
      id: Value(id),
      tenantId: Value(tenantId),
      name: Value(name),
      sportCode: Value(sportCode),
      status: Value(status),
      format:
          format == null && nullToAbsent ? const Value.absent() : Value(format),
      cachedAt: Value(cachedAt),
    );
  }

  factory CachedCompetition.fromJson(Map<String, dynamic> json,
      {ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return CachedCompetition(
      id: serializer.fromJson<String>(json['id']),
      tenantId: serializer.fromJson<String>(json['tenantId']),
      name: serializer.fromJson<String>(json['name']),
      sportCode: serializer.fromJson<String>(json['sportCode']),
      status: serializer.fromJson<String>(json['status']),
      format: serializer.fromJson<String?>(json['format']),
      cachedAt: serializer.fromJson<DateTime>(json['cachedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'tenantId': serializer.toJson<String>(tenantId),
      'name': serializer.toJson<String>(name),
      'sportCode': serializer.toJson<String>(sportCode),
      'status': serializer.toJson<String>(status),
      'format': serializer.toJson<String?>(format),
      'cachedAt': serializer.toJson<DateTime>(cachedAt),
    };
  }

  CachedCompetition copyWith(
          {String? id,
          String? tenantId,
          String? name,
          String? sportCode,
          String? status,
          Value<String?> format = const Value.absent(),
          DateTime? cachedAt}) =>
      CachedCompetition(
        id: id ?? this.id,
        tenantId: tenantId ?? this.tenantId,
        name: name ?? this.name,
        sportCode: sportCode ?? this.sportCode,
        status: status ?? this.status,
        format: format.present ? format.value : this.format,
        cachedAt: cachedAt ?? this.cachedAt,
      );
  CachedCompetition copyWithCompanion(CachedCompetitionsCompanion data) {
    return CachedCompetition(
      id: data.id.present ? data.id.value : this.id,
      tenantId: data.tenantId.present ? data.tenantId.value : this.tenantId,
      name: data.name.present ? data.name.value : this.name,
      sportCode: data.sportCode.present ? data.sportCode.value : this.sportCode,
      status: data.status.present ? data.status.value : this.status,
      format: data.format.present ? data.format.value : this.format,
      cachedAt: data.cachedAt.present ? data.cachedAt.value : this.cachedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('CachedCompetition(')
          ..write('id: $id, ')
          ..write('tenantId: $tenantId, ')
          ..write('name: $name, ')
          ..write('sportCode: $sportCode, ')
          ..write('status: $status, ')
          ..write('format: $format, ')
          ..write('cachedAt: $cachedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode =>
      Object.hash(id, tenantId, name, sportCode, status, format, cachedAt);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CachedCompetition &&
          other.id == this.id &&
          other.tenantId == this.tenantId &&
          other.name == this.name &&
          other.sportCode == this.sportCode &&
          other.status == this.status &&
          other.format == this.format &&
          other.cachedAt == this.cachedAt);
}

class CachedCompetitionsCompanion extends UpdateCompanion<CachedCompetition> {
  final Value<String> id;
  final Value<String> tenantId;
  final Value<String> name;
  final Value<String> sportCode;
  final Value<String> status;
  final Value<String?> format;
  final Value<DateTime> cachedAt;
  final Value<int> rowid;
  const CachedCompetitionsCompanion({
    this.id = const Value.absent(),
    this.tenantId = const Value.absent(),
    this.name = const Value.absent(),
    this.sportCode = const Value.absent(),
    this.status = const Value.absent(),
    this.format = const Value.absent(),
    this.cachedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  CachedCompetitionsCompanion.insert({
    required String id,
    required String tenantId,
    required String name,
    required String sportCode,
    required String status,
    this.format = const Value.absent(),
    required DateTime cachedAt,
    this.rowid = const Value.absent(),
  })  : id = Value(id),
        tenantId = Value(tenantId),
        name = Value(name),
        sportCode = Value(sportCode),
        status = Value(status),
        cachedAt = Value(cachedAt);
  static Insertable<CachedCompetition> custom({
    Expression<String>? id,
    Expression<String>? tenantId,
    Expression<String>? name,
    Expression<String>? sportCode,
    Expression<String>? status,
    Expression<String>? format,
    Expression<DateTime>? cachedAt,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (tenantId != null) 'tenant_id': tenantId,
      if (name != null) 'name': name,
      if (sportCode != null) 'sport_code': sportCode,
      if (status != null) 'status': status,
      if (format != null) 'format': format,
      if (cachedAt != null) 'cached_at': cachedAt,
      if (rowid != null) 'rowid': rowid,
    });
  }

  CachedCompetitionsCompanion copyWith(
      {Value<String>? id,
      Value<String>? tenantId,
      Value<String>? name,
      Value<String>? sportCode,
      Value<String>? status,
      Value<String?>? format,
      Value<DateTime>? cachedAt,
      Value<int>? rowid}) {
    return CachedCompetitionsCompanion(
      id: id ?? this.id,
      tenantId: tenantId ?? this.tenantId,
      name: name ?? this.name,
      sportCode: sportCode ?? this.sportCode,
      status: status ?? this.status,
      format: format ?? this.format,
      cachedAt: cachedAt ?? this.cachedAt,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (tenantId.present) {
      map['tenant_id'] = Variable<String>(tenantId.value);
    }
    if (name.present) {
      map['name'] = Variable<String>(name.value);
    }
    if (sportCode.present) {
      map['sport_code'] = Variable<String>(sportCode.value);
    }
    if (status.present) {
      map['status'] = Variable<String>(status.value);
    }
    if (format.present) {
      map['format'] = Variable<String>(format.value);
    }
    if (cachedAt.present) {
      map['cached_at'] = Variable<DateTime>(cachedAt.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('CachedCompetitionsCompanion(')
          ..write('id: $id, ')
          ..write('tenantId: $tenantId, ')
          ..write('name: $name, ')
          ..write('sportCode: $sportCode, ')
          ..write('status: $status, ')
          ..write('format: $format, ')
          ..write('cachedAt: $cachedAt, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $CachedSupporterResponsesTable extends CachedSupporterResponses
    with TableInfo<$CachedSupporterResponsesTable, CachedSupporterResponse> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $CachedSupporterResponsesTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _keyMeta = const VerificationMeta('key');
  @override
  late final GeneratedColumn<String> key = GeneratedColumn<String>(
      'key', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _tenantIdMeta =
      const VerificationMeta('tenantId');
  @override
  late final GeneratedColumn<String> tenantId = GeneratedColumn<String>(
      'tenant_id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _kindMeta = const VerificationMeta('kind');
  @override
  late final GeneratedColumn<String> kind = GeneratedColumn<String>(
      'kind', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _dataMeta = const VerificationMeta('data');
  @override
  late final GeneratedColumn<String> data = GeneratedColumn<String>(
      'data', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _cachedAtMeta =
      const VerificationMeta('cachedAt');
  @override
  late final GeneratedColumn<DateTime> cachedAt = GeneratedColumn<DateTime>(
      'cached_at', aliasedName, false,
      type: DriftSqlType.dateTime, requiredDuringInsert: true);
  @override
  List<GeneratedColumn> get $columns => [key, tenantId, kind, data, cachedAt];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'cached_supporter_responses';
  @override
  VerificationContext validateIntegrity(
      Insertable<CachedSupporterResponse> instance,
      {bool isInserting = false}) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('key')) {
      context.handle(
          _keyMeta, key.isAcceptableOrUnknown(data['key']!, _keyMeta));
    } else if (isInserting) {
      context.missing(_keyMeta);
    }
    if (data.containsKey('tenant_id')) {
      context.handle(_tenantIdMeta,
          tenantId.isAcceptableOrUnknown(data['tenant_id']!, _tenantIdMeta));
    } else if (isInserting) {
      context.missing(_tenantIdMeta);
    }
    if (data.containsKey('kind')) {
      context.handle(
          _kindMeta, kind.isAcceptableOrUnknown(data['kind']!, _kindMeta));
    } else if (isInserting) {
      context.missing(_kindMeta);
    }
    if (data.containsKey('data')) {
      context.handle(
          _dataMeta, this.data.isAcceptableOrUnknown(data['data']!, _dataMeta));
    } else if (isInserting) {
      context.missing(_dataMeta);
    }
    if (data.containsKey('cached_at')) {
      context.handle(_cachedAtMeta,
          cachedAt.isAcceptableOrUnknown(data['cached_at']!, _cachedAtMeta));
    } else if (isInserting) {
      context.missing(_cachedAtMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {key};
  @override
  CachedSupporterResponse map(Map<String, dynamic> data,
      {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return CachedSupporterResponse(
      key: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}key'])!,
      tenantId: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}tenant_id'])!,
      kind: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}kind'])!,
      data: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}data'])!,
      cachedAt: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}cached_at'])!,
    );
  }

  @override
  $CachedSupporterResponsesTable createAlias(String alias) {
    return $CachedSupporterResponsesTable(attachedDatabase, alias);
  }
}

class CachedSupporterResponse extends DataClass
    implements Insertable<CachedSupporterResponse> {
  final String key;
  final String tenantId;
  final String kind;
  final String data;
  final DateTime cachedAt;
  const CachedSupporterResponse(
      {required this.key,
      required this.tenantId,
      required this.kind,
      required this.data,
      required this.cachedAt});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['key'] = Variable<String>(key);
    map['tenant_id'] = Variable<String>(tenantId);
    map['kind'] = Variable<String>(kind);
    map['data'] = Variable<String>(data);
    map['cached_at'] = Variable<DateTime>(cachedAt);
    return map;
  }

  CachedSupporterResponsesCompanion toCompanion(bool nullToAbsent) {
    return CachedSupporterResponsesCompanion(
      key: Value(key),
      tenantId: Value(tenantId),
      kind: Value(kind),
      data: Value(data),
      cachedAt: Value(cachedAt),
    );
  }

  factory CachedSupporterResponse.fromJson(Map<String, dynamic> json,
      {ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return CachedSupporterResponse(
      key: serializer.fromJson<String>(json['key']),
      tenantId: serializer.fromJson<String>(json['tenantId']),
      kind: serializer.fromJson<String>(json['kind']),
      data: serializer.fromJson<String>(json['data']),
      cachedAt: serializer.fromJson<DateTime>(json['cachedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'key': serializer.toJson<String>(key),
      'tenantId': serializer.toJson<String>(tenantId),
      'kind': serializer.toJson<String>(kind),
      'data': serializer.toJson<String>(data),
      'cachedAt': serializer.toJson<DateTime>(cachedAt),
    };
  }

  CachedSupporterResponse copyWith(
          {String? key,
          String? tenantId,
          String? kind,
          String? data,
          DateTime? cachedAt}) =>
      CachedSupporterResponse(
        key: key ?? this.key,
        tenantId: tenantId ?? this.tenantId,
        kind: kind ?? this.kind,
        data: data ?? this.data,
        cachedAt: cachedAt ?? this.cachedAt,
      );
  CachedSupporterResponse copyWithCompanion(
      CachedSupporterResponsesCompanion data) {
    return CachedSupporterResponse(
      key: data.key.present ? data.key.value : this.key,
      tenantId: data.tenantId.present ? data.tenantId.value : this.tenantId,
      kind: data.kind.present ? data.kind.value : this.kind,
      data: data.data.present ? data.data.value : this.data,
      cachedAt: data.cachedAt.present ? data.cachedAt.value : this.cachedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('CachedSupporterResponse(')
          ..write('key: $key, ')
          ..write('tenantId: $tenantId, ')
          ..write('kind: $kind, ')
          ..write('data: $data, ')
          ..write('cachedAt: $cachedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(key, tenantId, kind, data, cachedAt);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CachedSupporterResponse &&
          other.key == this.key &&
          other.tenantId == this.tenantId &&
          other.kind == this.kind &&
          other.data == this.data &&
          other.cachedAt == this.cachedAt);
}

class CachedSupporterResponsesCompanion
    extends UpdateCompanion<CachedSupporterResponse> {
  final Value<String> key;
  final Value<String> tenantId;
  final Value<String> kind;
  final Value<String> data;
  final Value<DateTime> cachedAt;
  final Value<int> rowid;
  const CachedSupporterResponsesCompanion({
    this.key = const Value.absent(),
    this.tenantId = const Value.absent(),
    this.kind = const Value.absent(),
    this.data = const Value.absent(),
    this.cachedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  CachedSupporterResponsesCompanion.insert({
    required String key,
    required String tenantId,
    required String kind,
    required String data,
    required DateTime cachedAt,
    this.rowid = const Value.absent(),
  })  : key = Value(key),
        tenantId = Value(tenantId),
        kind = Value(kind),
        data = Value(data),
        cachedAt = Value(cachedAt);
  static Insertable<CachedSupporterResponse> custom({
    Expression<String>? key,
    Expression<String>? tenantId,
    Expression<String>? kind,
    Expression<String>? data,
    Expression<DateTime>? cachedAt,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (key != null) 'key': key,
      if (tenantId != null) 'tenant_id': tenantId,
      if (kind != null) 'kind': kind,
      if (data != null) 'data': data,
      if (cachedAt != null) 'cached_at': cachedAt,
      if (rowid != null) 'rowid': rowid,
    });
  }

  CachedSupporterResponsesCompanion copyWith(
      {Value<String>? key,
      Value<String>? tenantId,
      Value<String>? kind,
      Value<String>? data,
      Value<DateTime>? cachedAt,
      Value<int>? rowid}) {
    return CachedSupporterResponsesCompanion(
      key: key ?? this.key,
      tenantId: tenantId ?? this.tenantId,
      kind: kind ?? this.kind,
      data: data ?? this.data,
      cachedAt: cachedAt ?? this.cachedAt,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (key.present) {
      map['key'] = Variable<String>(key.value);
    }
    if (tenantId.present) {
      map['tenant_id'] = Variable<String>(tenantId.value);
    }
    if (kind.present) {
      map['kind'] = Variable<String>(kind.value);
    }
    if (data.present) {
      map['data'] = Variable<String>(data.value);
    }
    if (cachedAt.present) {
      map['cached_at'] = Variable<DateTime>(cachedAt.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('CachedSupporterResponsesCompanion(')
          ..write('key: $key, ')
          ..write('tenantId: $tenantId, ')
          ..write('kind: $kind, ')
          ..write('data: $data, ')
          ..write('cachedAt: $cachedAt, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $ScoringSessionsTable extends ScoringSessions
    with TableInfo<$ScoringSessionsTable, ScoringSession> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $ScoringSessionsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
      'id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _contestIdMeta =
      const VerificationMeta('contestId');
  @override
  late final GeneratedColumn<String> contestId = GeneratedColumn<String>(
      'contest_id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _userIdMeta = const VerificationMeta('userId');
  @override
  late final GeneratedColumn<String> userId = GeneratedColumn<String>(
      'user_id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _tenantIdMeta =
      const VerificationMeta('tenantId');
  @override
  late final GeneratedColumn<String> tenantId = GeneratedColumn<String>(
      'tenant_id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _sportCodeMeta =
      const VerificationMeta('sportCode');
  @override
  late final GeneratedColumn<String> sportCode = GeneratedColumn<String>(
      'sport_code', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _statusMeta = const VerificationMeta('status');
  @override
  late final GeneratedColumn<String> status = GeneratedColumn<String>(
      'status', aliasedName, false,
      type: DriftSqlType.string,
      requiredDuringInsert: false,
      defaultValue: const Constant('active'));
  static const VerificationMeta _startedAtMeta =
      const VerificationMeta('startedAt');
  @override
  late final GeneratedColumn<DateTime> startedAt = GeneratedColumn<DateTime>(
      'started_at', aliasedName, false,
      type: DriftSqlType.dateTime, requiredDuringInsert: true);
  static const VerificationMeta _lastUpdatedAtMeta =
      const VerificationMeta('lastUpdatedAt');
  @override
  late final GeneratedColumn<DateTime> lastUpdatedAt =
      GeneratedColumn<DateTime>('last_updated_at', aliasedName, false,
          type: DriftSqlType.dateTime, requiredDuringInsert: true);
  static const VerificationMeta _currentStateMeta =
      const VerificationMeta('currentState');
  @override
  late final GeneratedColumn<String> currentState = GeneratedColumn<String>(
      'current_state', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _syncedEventsCountMeta =
      const VerificationMeta('syncedEventsCount');
  @override
  late final GeneratedColumn<int> syncedEventsCount = GeneratedColumn<int>(
      'synced_events_count', aliasedName, false,
      type: DriftSqlType.int,
      requiredDuringInsert: false,
      defaultValue: const Constant(0));
  @override
  List<GeneratedColumn> get $columns => [
        id,
        contestId,
        userId,
        tenantId,
        sportCode,
        status,
        startedAt,
        lastUpdatedAt,
        currentState,
        syncedEventsCount
      ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'scoring_sessions';
  @override
  VerificationContext validateIntegrity(Insertable<ScoringSession> instance,
      {bool isInserting = false}) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('contest_id')) {
      context.handle(_contestIdMeta,
          contestId.isAcceptableOrUnknown(data['contest_id']!, _contestIdMeta));
    } else if (isInserting) {
      context.missing(_contestIdMeta);
    }
    if (data.containsKey('user_id')) {
      context.handle(_userIdMeta,
          userId.isAcceptableOrUnknown(data['user_id']!, _userIdMeta));
    } else if (isInserting) {
      context.missing(_userIdMeta);
    }
    if (data.containsKey('tenant_id')) {
      context.handle(_tenantIdMeta,
          tenantId.isAcceptableOrUnknown(data['tenant_id']!, _tenantIdMeta));
    } else if (isInserting) {
      context.missing(_tenantIdMeta);
    }
    if (data.containsKey('sport_code')) {
      context.handle(_sportCodeMeta,
          sportCode.isAcceptableOrUnknown(data['sport_code']!, _sportCodeMeta));
    } else if (isInserting) {
      context.missing(_sportCodeMeta);
    }
    if (data.containsKey('status')) {
      context.handle(_statusMeta,
          status.isAcceptableOrUnknown(data['status']!, _statusMeta));
    }
    if (data.containsKey('started_at')) {
      context.handle(_startedAtMeta,
          startedAt.isAcceptableOrUnknown(data['started_at']!, _startedAtMeta));
    } else if (isInserting) {
      context.missing(_startedAtMeta);
    }
    if (data.containsKey('last_updated_at')) {
      context.handle(
          _lastUpdatedAtMeta,
          lastUpdatedAt.isAcceptableOrUnknown(
              data['last_updated_at']!, _lastUpdatedAtMeta));
    } else if (isInserting) {
      context.missing(_lastUpdatedAtMeta);
    }
    if (data.containsKey('current_state')) {
      context.handle(
          _currentStateMeta,
          currentState.isAcceptableOrUnknown(
              data['current_state']!, _currentStateMeta));
    } else if (isInserting) {
      context.missing(_currentStateMeta);
    }
    if (data.containsKey('synced_events_count')) {
      context.handle(
          _syncedEventsCountMeta,
          syncedEventsCount.isAcceptableOrUnknown(
              data['synced_events_count']!, _syncedEventsCountMeta));
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  ScoringSession map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return ScoringSession(
      id: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}id'])!,
      contestId: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}contest_id'])!,
      userId: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}user_id'])!,
      tenantId: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}tenant_id'])!,
      sportCode: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}sport_code'])!,
      status: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}status'])!,
      startedAt: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}started_at'])!,
      lastUpdatedAt: attachedDatabase.typeMapping.read(
          DriftSqlType.dateTime, data['${effectivePrefix}last_updated_at'])!,
      currentState: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}current_state'])!,
      syncedEventsCount: attachedDatabase.typeMapping.read(
          DriftSqlType.int, data['${effectivePrefix}synced_events_count'])!,
    );
  }

  @override
  $ScoringSessionsTable createAlias(String alias) {
    return $ScoringSessionsTable(attachedDatabase, alias);
  }
}

class ScoringSession extends DataClass implements Insertable<ScoringSession> {
  final String id;
  final String contestId;
  final String userId;
  final String tenantId;
  final String sportCode;
  final String status;
  final DateTime startedAt;
  final DateTime lastUpdatedAt;
  final String currentState;
  final int syncedEventsCount;
  const ScoringSession(
      {required this.id,
      required this.contestId,
      required this.userId,
      required this.tenantId,
      required this.sportCode,
      required this.status,
      required this.startedAt,
      required this.lastUpdatedAt,
      required this.currentState,
      required this.syncedEventsCount});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['contest_id'] = Variable<String>(contestId);
    map['user_id'] = Variable<String>(userId);
    map['tenant_id'] = Variable<String>(tenantId);
    map['sport_code'] = Variable<String>(sportCode);
    map['status'] = Variable<String>(status);
    map['started_at'] = Variable<DateTime>(startedAt);
    map['last_updated_at'] = Variable<DateTime>(lastUpdatedAt);
    map['current_state'] = Variable<String>(currentState);
    map['synced_events_count'] = Variable<int>(syncedEventsCount);
    return map;
  }

  ScoringSessionsCompanion toCompanion(bool nullToAbsent) {
    return ScoringSessionsCompanion(
      id: Value(id),
      contestId: Value(contestId),
      userId: Value(userId),
      tenantId: Value(tenantId),
      sportCode: Value(sportCode),
      status: Value(status),
      startedAt: Value(startedAt),
      lastUpdatedAt: Value(lastUpdatedAt),
      currentState: Value(currentState),
      syncedEventsCount: Value(syncedEventsCount),
    );
  }

  factory ScoringSession.fromJson(Map<String, dynamic> json,
      {ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return ScoringSession(
      id: serializer.fromJson<String>(json['id']),
      contestId: serializer.fromJson<String>(json['contestId']),
      userId: serializer.fromJson<String>(json['userId']),
      tenantId: serializer.fromJson<String>(json['tenantId']),
      sportCode: serializer.fromJson<String>(json['sportCode']),
      status: serializer.fromJson<String>(json['status']),
      startedAt: serializer.fromJson<DateTime>(json['startedAt']),
      lastUpdatedAt: serializer.fromJson<DateTime>(json['lastUpdatedAt']),
      currentState: serializer.fromJson<String>(json['currentState']),
      syncedEventsCount: serializer.fromJson<int>(json['syncedEventsCount']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'contestId': serializer.toJson<String>(contestId),
      'userId': serializer.toJson<String>(userId),
      'tenantId': serializer.toJson<String>(tenantId),
      'sportCode': serializer.toJson<String>(sportCode),
      'status': serializer.toJson<String>(status),
      'startedAt': serializer.toJson<DateTime>(startedAt),
      'lastUpdatedAt': serializer.toJson<DateTime>(lastUpdatedAt),
      'currentState': serializer.toJson<String>(currentState),
      'syncedEventsCount': serializer.toJson<int>(syncedEventsCount),
    };
  }

  ScoringSession copyWith(
          {String? id,
          String? contestId,
          String? userId,
          String? tenantId,
          String? sportCode,
          String? status,
          DateTime? startedAt,
          DateTime? lastUpdatedAt,
          String? currentState,
          int? syncedEventsCount}) =>
      ScoringSession(
        id: id ?? this.id,
        contestId: contestId ?? this.contestId,
        userId: userId ?? this.userId,
        tenantId: tenantId ?? this.tenantId,
        sportCode: sportCode ?? this.sportCode,
        status: status ?? this.status,
        startedAt: startedAt ?? this.startedAt,
        lastUpdatedAt: lastUpdatedAt ?? this.lastUpdatedAt,
        currentState: currentState ?? this.currentState,
        syncedEventsCount: syncedEventsCount ?? this.syncedEventsCount,
      );
  ScoringSession copyWithCompanion(ScoringSessionsCompanion data) {
    return ScoringSession(
      id: data.id.present ? data.id.value : this.id,
      contestId: data.contestId.present ? data.contestId.value : this.contestId,
      userId: data.userId.present ? data.userId.value : this.userId,
      tenantId: data.tenantId.present ? data.tenantId.value : this.tenantId,
      sportCode: data.sportCode.present ? data.sportCode.value : this.sportCode,
      status: data.status.present ? data.status.value : this.status,
      startedAt: data.startedAt.present ? data.startedAt.value : this.startedAt,
      lastUpdatedAt: data.lastUpdatedAt.present
          ? data.lastUpdatedAt.value
          : this.lastUpdatedAt,
      currentState: data.currentState.present
          ? data.currentState.value
          : this.currentState,
      syncedEventsCount: data.syncedEventsCount.present
          ? data.syncedEventsCount.value
          : this.syncedEventsCount,
    );
  }

  @override
  String toString() {
    return (StringBuffer('ScoringSession(')
          ..write('id: $id, ')
          ..write('contestId: $contestId, ')
          ..write('userId: $userId, ')
          ..write('tenantId: $tenantId, ')
          ..write('sportCode: $sportCode, ')
          ..write('status: $status, ')
          ..write('startedAt: $startedAt, ')
          ..write('lastUpdatedAt: $lastUpdatedAt, ')
          ..write('currentState: $currentState, ')
          ..write('syncedEventsCount: $syncedEventsCount')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(id, contestId, userId, tenantId, sportCode,
      status, startedAt, lastUpdatedAt, currentState, syncedEventsCount);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is ScoringSession &&
          other.id == this.id &&
          other.contestId == this.contestId &&
          other.userId == this.userId &&
          other.tenantId == this.tenantId &&
          other.sportCode == this.sportCode &&
          other.status == this.status &&
          other.startedAt == this.startedAt &&
          other.lastUpdatedAt == this.lastUpdatedAt &&
          other.currentState == this.currentState &&
          other.syncedEventsCount == this.syncedEventsCount);
}

class ScoringSessionsCompanion extends UpdateCompanion<ScoringSession> {
  final Value<String> id;
  final Value<String> contestId;
  final Value<String> userId;
  final Value<String> tenantId;
  final Value<String> sportCode;
  final Value<String> status;
  final Value<DateTime> startedAt;
  final Value<DateTime> lastUpdatedAt;
  final Value<String> currentState;
  final Value<int> syncedEventsCount;
  final Value<int> rowid;
  const ScoringSessionsCompanion({
    this.id = const Value.absent(),
    this.contestId = const Value.absent(),
    this.userId = const Value.absent(),
    this.tenantId = const Value.absent(),
    this.sportCode = const Value.absent(),
    this.status = const Value.absent(),
    this.startedAt = const Value.absent(),
    this.lastUpdatedAt = const Value.absent(),
    this.currentState = const Value.absent(),
    this.syncedEventsCount = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  ScoringSessionsCompanion.insert({
    required String id,
    required String contestId,
    required String userId,
    required String tenantId,
    required String sportCode,
    this.status = const Value.absent(),
    required DateTime startedAt,
    required DateTime lastUpdatedAt,
    required String currentState,
    this.syncedEventsCount = const Value.absent(),
    this.rowid = const Value.absent(),
  })  : id = Value(id),
        contestId = Value(contestId),
        userId = Value(userId),
        tenantId = Value(tenantId),
        sportCode = Value(sportCode),
        startedAt = Value(startedAt),
        lastUpdatedAt = Value(lastUpdatedAt),
        currentState = Value(currentState);
  static Insertable<ScoringSession> custom({
    Expression<String>? id,
    Expression<String>? contestId,
    Expression<String>? userId,
    Expression<String>? tenantId,
    Expression<String>? sportCode,
    Expression<String>? status,
    Expression<DateTime>? startedAt,
    Expression<DateTime>? lastUpdatedAt,
    Expression<String>? currentState,
    Expression<int>? syncedEventsCount,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (contestId != null) 'contest_id': contestId,
      if (userId != null) 'user_id': userId,
      if (tenantId != null) 'tenant_id': tenantId,
      if (sportCode != null) 'sport_code': sportCode,
      if (status != null) 'status': status,
      if (startedAt != null) 'started_at': startedAt,
      if (lastUpdatedAt != null) 'last_updated_at': lastUpdatedAt,
      if (currentState != null) 'current_state': currentState,
      if (syncedEventsCount != null) 'synced_events_count': syncedEventsCount,
      if (rowid != null) 'rowid': rowid,
    });
  }

  ScoringSessionsCompanion copyWith(
      {Value<String>? id,
      Value<String>? contestId,
      Value<String>? userId,
      Value<String>? tenantId,
      Value<String>? sportCode,
      Value<String>? status,
      Value<DateTime>? startedAt,
      Value<DateTime>? lastUpdatedAt,
      Value<String>? currentState,
      Value<int>? syncedEventsCount,
      Value<int>? rowid}) {
    return ScoringSessionsCompanion(
      id: id ?? this.id,
      contestId: contestId ?? this.contestId,
      userId: userId ?? this.userId,
      tenantId: tenantId ?? this.tenantId,
      sportCode: sportCode ?? this.sportCode,
      status: status ?? this.status,
      startedAt: startedAt ?? this.startedAt,
      lastUpdatedAt: lastUpdatedAt ?? this.lastUpdatedAt,
      currentState: currentState ?? this.currentState,
      syncedEventsCount: syncedEventsCount ?? this.syncedEventsCount,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (contestId.present) {
      map['contest_id'] = Variable<String>(contestId.value);
    }
    if (userId.present) {
      map['user_id'] = Variable<String>(userId.value);
    }
    if (tenantId.present) {
      map['tenant_id'] = Variable<String>(tenantId.value);
    }
    if (sportCode.present) {
      map['sport_code'] = Variable<String>(sportCode.value);
    }
    if (status.present) {
      map['status'] = Variable<String>(status.value);
    }
    if (startedAt.present) {
      map['started_at'] = Variable<DateTime>(startedAt.value);
    }
    if (lastUpdatedAt.present) {
      map['last_updated_at'] = Variable<DateTime>(lastUpdatedAt.value);
    }
    if (currentState.present) {
      map['current_state'] = Variable<String>(currentState.value);
    }
    if (syncedEventsCount.present) {
      map['synced_events_count'] = Variable<int>(syncedEventsCount.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('ScoringSessionsCompanion(')
          ..write('id: $id, ')
          ..write('contestId: $contestId, ')
          ..write('userId: $userId, ')
          ..write('tenantId: $tenantId, ')
          ..write('sportCode: $sportCode, ')
          ..write('status: $status, ')
          ..write('startedAt: $startedAt, ')
          ..write('lastUpdatedAt: $lastUpdatedAt, ')
          ..write('currentState: $currentState, ')
          ..write('syncedEventsCount: $syncedEventsCount, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $UserSessionsTable extends UserSessions
    with TableInfo<$UserSessionsTable, UserSession> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $UserSessionsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
      'id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _userIdMeta = const VerificationMeta('userId');
  @override
  late final GeneratedColumn<String> userId = GeneratedColumn<String>(
      'user_id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _tenantIdMeta =
      const VerificationMeta('tenantId');
  @override
  late final GeneratedColumn<String> tenantId = GeneratedColumn<String>(
      'tenant_id', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _emailMeta = const VerificationMeta('email');
  @override
  late final GeneratedColumn<String> email = GeneratedColumn<String>(
      'email', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _displayNameMeta =
      const VerificationMeta('displayName');
  @override
  late final GeneratedColumn<String> displayName = GeneratedColumn<String>(
      'display_name', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _roleMeta = const VerificationMeta('role');
  @override
  late final GeneratedColumn<String> role = GeneratedColumn<String>(
      'role', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _accessTokenMeta =
      const VerificationMeta('accessToken');
  @override
  late final GeneratedColumn<String> accessToken = GeneratedColumn<String>(
      'access_token', aliasedName, false,
      type: DriftSqlType.string, requiredDuringInsert: true);
  static const VerificationMeta _refreshTokenMeta =
      const VerificationMeta('refreshToken');
  @override
  late final GeneratedColumn<String> refreshToken = GeneratedColumn<String>(
      'refresh_token', aliasedName, true,
      type: DriftSqlType.string, requiredDuringInsert: false);
  static const VerificationMeta _expiresAtMeta =
      const VerificationMeta('expiresAt');
  @override
  late final GeneratedColumn<DateTime> expiresAt = GeneratedColumn<DateTime>(
      'expires_at', aliasedName, false,
      type: DriftSqlType.dateTime, requiredDuringInsert: true);
  static const VerificationMeta _createdAtMeta =
      const VerificationMeta('createdAt');
  @override
  late final GeneratedColumn<DateTime> createdAt = GeneratedColumn<DateTime>(
      'created_at', aliasedName, false,
      type: DriftSqlType.dateTime, requiredDuringInsert: true);
  @override
  List<GeneratedColumn> get $columns => [
        id,
        userId,
        tenantId,
        email,
        displayName,
        role,
        accessToken,
        refreshToken,
        expiresAt,
        createdAt
      ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'user_sessions';
  @override
  VerificationContext validateIntegrity(Insertable<UserSession> instance,
      {bool isInserting = false}) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('user_id')) {
      context.handle(_userIdMeta,
          userId.isAcceptableOrUnknown(data['user_id']!, _userIdMeta));
    } else if (isInserting) {
      context.missing(_userIdMeta);
    }
    if (data.containsKey('tenant_id')) {
      context.handle(_tenantIdMeta,
          tenantId.isAcceptableOrUnknown(data['tenant_id']!, _tenantIdMeta));
    } else if (isInserting) {
      context.missing(_tenantIdMeta);
    }
    if (data.containsKey('email')) {
      context.handle(
          _emailMeta, email.isAcceptableOrUnknown(data['email']!, _emailMeta));
    } else if (isInserting) {
      context.missing(_emailMeta);
    }
    if (data.containsKey('display_name')) {
      context.handle(
          _displayNameMeta,
          displayName.isAcceptableOrUnknown(
              data['display_name']!, _displayNameMeta));
    } else if (isInserting) {
      context.missing(_displayNameMeta);
    }
    if (data.containsKey('role')) {
      context.handle(
          _roleMeta, role.isAcceptableOrUnknown(data['role']!, _roleMeta));
    } else if (isInserting) {
      context.missing(_roleMeta);
    }
    if (data.containsKey('access_token')) {
      context.handle(
          _accessTokenMeta,
          accessToken.isAcceptableOrUnknown(
              data['access_token']!, _accessTokenMeta));
    } else if (isInserting) {
      context.missing(_accessTokenMeta);
    }
    if (data.containsKey('refresh_token')) {
      context.handle(
          _refreshTokenMeta,
          refreshToken.isAcceptableOrUnknown(
              data['refresh_token']!, _refreshTokenMeta));
    }
    if (data.containsKey('expires_at')) {
      context.handle(_expiresAtMeta,
          expiresAt.isAcceptableOrUnknown(data['expires_at']!, _expiresAtMeta));
    } else if (isInserting) {
      context.missing(_expiresAtMeta);
    }
    if (data.containsKey('created_at')) {
      context.handle(_createdAtMeta,
          createdAt.isAcceptableOrUnknown(data['created_at']!, _createdAtMeta));
    } else if (isInserting) {
      context.missing(_createdAtMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  UserSession map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return UserSession(
      id: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}id'])!,
      userId: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}user_id'])!,
      tenantId: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}tenant_id'])!,
      email: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}email'])!,
      displayName: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}display_name'])!,
      role: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}role'])!,
      accessToken: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}access_token'])!,
      refreshToken: attachedDatabase.typeMapping
          .read(DriftSqlType.string, data['${effectivePrefix}refresh_token']),
      expiresAt: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}expires_at'])!,
      createdAt: attachedDatabase.typeMapping
          .read(DriftSqlType.dateTime, data['${effectivePrefix}created_at'])!,
    );
  }

  @override
  $UserSessionsTable createAlias(String alias) {
    return $UserSessionsTable(attachedDatabase, alias);
  }
}

class UserSession extends DataClass implements Insertable<UserSession> {
  final String id;
  final String userId;
  final String tenantId;
  final String email;
  final String displayName;
  final String role;
  final String accessToken;
  final String? refreshToken;
  final DateTime expiresAt;
  final DateTime createdAt;
  const UserSession(
      {required this.id,
      required this.userId,
      required this.tenantId,
      required this.email,
      required this.displayName,
      required this.role,
      required this.accessToken,
      this.refreshToken,
      required this.expiresAt,
      required this.createdAt});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['user_id'] = Variable<String>(userId);
    map['tenant_id'] = Variable<String>(tenantId);
    map['email'] = Variable<String>(email);
    map['display_name'] = Variable<String>(displayName);
    map['role'] = Variable<String>(role);
    map['access_token'] = Variable<String>(accessToken);
    if (!nullToAbsent || refreshToken != null) {
      map['refresh_token'] = Variable<String>(refreshToken);
    }
    map['expires_at'] = Variable<DateTime>(expiresAt);
    map['created_at'] = Variable<DateTime>(createdAt);
    return map;
  }

  UserSessionsCompanion toCompanion(bool nullToAbsent) {
    return UserSessionsCompanion(
      id: Value(id),
      userId: Value(userId),
      tenantId: Value(tenantId),
      email: Value(email),
      displayName: Value(displayName),
      role: Value(role),
      accessToken: Value(accessToken),
      refreshToken: refreshToken == null && nullToAbsent
          ? const Value.absent()
          : Value(refreshToken),
      expiresAt: Value(expiresAt),
      createdAt: Value(createdAt),
    );
  }

  factory UserSession.fromJson(Map<String, dynamic> json,
      {ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return UserSession(
      id: serializer.fromJson<String>(json['id']),
      userId: serializer.fromJson<String>(json['userId']),
      tenantId: serializer.fromJson<String>(json['tenantId']),
      email: serializer.fromJson<String>(json['email']),
      displayName: serializer.fromJson<String>(json['displayName']),
      role: serializer.fromJson<String>(json['role']),
      accessToken: serializer.fromJson<String>(json['accessToken']),
      refreshToken: serializer.fromJson<String?>(json['refreshToken']),
      expiresAt: serializer.fromJson<DateTime>(json['expiresAt']),
      createdAt: serializer.fromJson<DateTime>(json['createdAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'userId': serializer.toJson<String>(userId),
      'tenantId': serializer.toJson<String>(tenantId),
      'email': serializer.toJson<String>(email),
      'displayName': serializer.toJson<String>(displayName),
      'role': serializer.toJson<String>(role),
      'accessToken': serializer.toJson<String>(accessToken),
      'refreshToken': serializer.toJson<String?>(refreshToken),
      'expiresAt': serializer.toJson<DateTime>(expiresAt),
      'createdAt': serializer.toJson<DateTime>(createdAt),
    };
  }

  UserSession copyWith(
          {String? id,
          String? userId,
          String? tenantId,
          String? email,
          String? displayName,
          String? role,
          String? accessToken,
          Value<String?> refreshToken = const Value.absent(),
          DateTime? expiresAt,
          DateTime? createdAt}) =>
      UserSession(
        id: id ?? this.id,
        userId: userId ?? this.userId,
        tenantId: tenantId ?? this.tenantId,
        email: email ?? this.email,
        displayName: displayName ?? this.displayName,
        role: role ?? this.role,
        accessToken: accessToken ?? this.accessToken,
        refreshToken:
            refreshToken.present ? refreshToken.value : this.refreshToken,
        expiresAt: expiresAt ?? this.expiresAt,
        createdAt: createdAt ?? this.createdAt,
      );
  UserSession copyWithCompanion(UserSessionsCompanion data) {
    return UserSession(
      id: data.id.present ? data.id.value : this.id,
      userId: data.userId.present ? data.userId.value : this.userId,
      tenantId: data.tenantId.present ? data.tenantId.value : this.tenantId,
      email: data.email.present ? data.email.value : this.email,
      displayName:
          data.displayName.present ? data.displayName.value : this.displayName,
      role: data.role.present ? data.role.value : this.role,
      accessToken:
          data.accessToken.present ? data.accessToken.value : this.accessToken,
      refreshToken: data.refreshToken.present
          ? data.refreshToken.value
          : this.refreshToken,
      expiresAt: data.expiresAt.present ? data.expiresAt.value : this.expiresAt,
      createdAt: data.createdAt.present ? data.createdAt.value : this.createdAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('UserSession(')
          ..write('id: $id, ')
          ..write('userId: $userId, ')
          ..write('tenantId: $tenantId, ')
          ..write('email: $email, ')
          ..write('displayName: $displayName, ')
          ..write('role: $role, ')
          ..write('accessToken: $accessToken, ')
          ..write('refreshToken: $refreshToken, ')
          ..write('expiresAt: $expiresAt, ')
          ..write('createdAt: $createdAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(id, userId, tenantId, email, displayName,
      role, accessToken, refreshToken, expiresAt, createdAt);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is UserSession &&
          other.id == this.id &&
          other.userId == this.userId &&
          other.tenantId == this.tenantId &&
          other.email == this.email &&
          other.displayName == this.displayName &&
          other.role == this.role &&
          other.accessToken == this.accessToken &&
          other.refreshToken == this.refreshToken &&
          other.expiresAt == this.expiresAt &&
          other.createdAt == this.createdAt);
}

class UserSessionsCompanion extends UpdateCompanion<UserSession> {
  final Value<String> id;
  final Value<String> userId;
  final Value<String> tenantId;
  final Value<String> email;
  final Value<String> displayName;
  final Value<String> role;
  final Value<String> accessToken;
  final Value<String?> refreshToken;
  final Value<DateTime> expiresAt;
  final Value<DateTime> createdAt;
  final Value<int> rowid;
  const UserSessionsCompanion({
    this.id = const Value.absent(),
    this.userId = const Value.absent(),
    this.tenantId = const Value.absent(),
    this.email = const Value.absent(),
    this.displayName = const Value.absent(),
    this.role = const Value.absent(),
    this.accessToken = const Value.absent(),
    this.refreshToken = const Value.absent(),
    this.expiresAt = const Value.absent(),
    this.createdAt = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  UserSessionsCompanion.insert({
    required String id,
    required String userId,
    required String tenantId,
    required String email,
    required String displayName,
    required String role,
    required String accessToken,
    this.refreshToken = const Value.absent(),
    required DateTime expiresAt,
    required DateTime createdAt,
    this.rowid = const Value.absent(),
  })  : id = Value(id),
        userId = Value(userId),
        tenantId = Value(tenantId),
        email = Value(email),
        displayName = Value(displayName),
        role = Value(role),
        accessToken = Value(accessToken),
        expiresAt = Value(expiresAt),
        createdAt = Value(createdAt);
  static Insertable<UserSession> custom({
    Expression<String>? id,
    Expression<String>? userId,
    Expression<String>? tenantId,
    Expression<String>? email,
    Expression<String>? displayName,
    Expression<String>? role,
    Expression<String>? accessToken,
    Expression<String>? refreshToken,
    Expression<DateTime>? expiresAt,
    Expression<DateTime>? createdAt,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (userId != null) 'user_id': userId,
      if (tenantId != null) 'tenant_id': tenantId,
      if (email != null) 'email': email,
      if (displayName != null) 'display_name': displayName,
      if (role != null) 'role': role,
      if (accessToken != null) 'access_token': accessToken,
      if (refreshToken != null) 'refresh_token': refreshToken,
      if (expiresAt != null) 'expires_at': expiresAt,
      if (createdAt != null) 'created_at': createdAt,
      if (rowid != null) 'rowid': rowid,
    });
  }

  UserSessionsCompanion copyWith(
      {Value<String>? id,
      Value<String>? userId,
      Value<String>? tenantId,
      Value<String>? email,
      Value<String>? displayName,
      Value<String>? role,
      Value<String>? accessToken,
      Value<String?>? refreshToken,
      Value<DateTime>? expiresAt,
      Value<DateTime>? createdAt,
      Value<int>? rowid}) {
    return UserSessionsCompanion(
      id: id ?? this.id,
      userId: userId ?? this.userId,
      tenantId: tenantId ?? this.tenantId,
      email: email ?? this.email,
      displayName: displayName ?? this.displayName,
      role: role ?? this.role,
      accessToken: accessToken ?? this.accessToken,
      refreshToken: refreshToken ?? this.refreshToken,
      expiresAt: expiresAt ?? this.expiresAt,
      createdAt: createdAt ?? this.createdAt,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (userId.present) {
      map['user_id'] = Variable<String>(userId.value);
    }
    if (tenantId.present) {
      map['tenant_id'] = Variable<String>(tenantId.value);
    }
    if (email.present) {
      map['email'] = Variable<String>(email.value);
    }
    if (displayName.present) {
      map['display_name'] = Variable<String>(displayName.value);
    }
    if (role.present) {
      map['role'] = Variable<String>(role.value);
    }
    if (accessToken.present) {
      map['access_token'] = Variable<String>(accessToken.value);
    }
    if (refreshToken.present) {
      map['refresh_token'] = Variable<String>(refreshToken.value);
    }
    if (expiresAt.present) {
      map['expires_at'] = Variable<DateTime>(expiresAt.value);
    }
    if (createdAt.present) {
      map['created_at'] = Variable<DateTime>(createdAt.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('UserSessionsCompanion(')
          ..write('id: $id, ')
          ..write('userId: $userId, ')
          ..write('tenantId: $tenantId, ')
          ..write('email: $email, ')
          ..write('displayName: $displayName, ')
          ..write('role: $role, ')
          ..write('accessToken: $accessToken, ')
          ..write('refreshToken: $refreshToken, ')
          ..write('expiresAt: $expiresAt, ')
          ..write('createdAt: $createdAt, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

abstract class _$AppDatabase extends GeneratedDatabase {
  _$AppDatabase(QueryExecutor e) : super(e);
  $AppDatabaseManager get managers => $AppDatabaseManager(this);
  late final $OfflineEventsTable offlineEvents = $OfflineEventsTable(this);
  late final $CachedContestsTable cachedContests = $CachedContestsTable(this);
  late final $CachedParticipantsTable cachedParticipants =
      $CachedParticipantsTable(this);
  late final $CachedCompetitionsTable cachedCompetitions =
      $CachedCompetitionsTable(this);
  late final $CachedSupporterResponsesTable cachedSupporterResponses =
      $CachedSupporterResponsesTable(this);
  late final $ScoringSessionsTable scoringSessions =
      $ScoringSessionsTable(this);
  late final $UserSessionsTable userSessions = $UserSessionsTable(this);
  @override
  Iterable<TableInfo<Table, Object?>> get allTables =>
      allSchemaEntities.whereType<TableInfo<Table, Object?>>();
  @override
  List<DatabaseSchemaEntity> get allSchemaEntities => [
        offlineEvents,
        cachedContests,
        cachedParticipants,
        cachedCompetitions,
        cachedSupporterResponses,
        scoringSessions,
        userSessions
      ];
}

typedef $$OfflineEventsTableCreateCompanionBuilder = OfflineEventsCompanion
    Function({
  required String id,
  required String type,
  required String endpoint,
  required String method,
  required String data,
  required DateTime createdAt,
  required int sequenceNumber,
  Value<String> status,
  Value<String?> errorMessage,
  Value<int> retryCount,
  Value<int> rowid,
});
typedef $$OfflineEventsTableUpdateCompanionBuilder = OfflineEventsCompanion
    Function({
  Value<String> id,
  Value<String> type,
  Value<String> endpoint,
  Value<String> method,
  Value<String> data,
  Value<DateTime> createdAt,
  Value<int> sequenceNumber,
  Value<String> status,
  Value<String?> errorMessage,
  Value<int> retryCount,
  Value<int> rowid,
});

class $$OfflineEventsTableTableManager extends RootTableManager<
    _$AppDatabase,
    $OfflineEventsTable,
    OfflineEvent,
    $$OfflineEventsTableFilterComposer,
    $$OfflineEventsTableOrderingComposer,
    $$OfflineEventsTableCreateCompanionBuilder,
    $$OfflineEventsTableUpdateCompanionBuilder> {
  $$OfflineEventsTableTableManager(_$AppDatabase db, $OfflineEventsTable table)
      : super(TableManagerState(
          db: db,
          table: table,
          filteringComposer:
              $$OfflineEventsTableFilterComposer(ComposerState(db, table)),
          orderingComposer:
              $$OfflineEventsTableOrderingComposer(ComposerState(db, table)),
          updateCompanionCallback: ({
            Value<String> id = const Value.absent(),
            Value<String> type = const Value.absent(),
            Value<String> endpoint = const Value.absent(),
            Value<String> method = const Value.absent(),
            Value<String> data = const Value.absent(),
            Value<DateTime> createdAt = const Value.absent(),
            Value<int> sequenceNumber = const Value.absent(),
            Value<String> status = const Value.absent(),
            Value<String?> errorMessage = const Value.absent(),
            Value<int> retryCount = const Value.absent(),
            Value<int> rowid = const Value.absent(),
          }) =>
              OfflineEventsCompanion(
            id: id,
            type: type,
            endpoint: endpoint,
            method: method,
            data: data,
            createdAt: createdAt,
            sequenceNumber: sequenceNumber,
            status: status,
            errorMessage: errorMessage,
            retryCount: retryCount,
            rowid: rowid,
          ),
          createCompanionCallback: ({
            required String id,
            required String type,
            required String endpoint,
            required String method,
            required String data,
            required DateTime createdAt,
            required int sequenceNumber,
            Value<String> status = const Value.absent(),
            Value<String?> errorMessage = const Value.absent(),
            Value<int> retryCount = const Value.absent(),
            Value<int> rowid = const Value.absent(),
          }) =>
              OfflineEventsCompanion.insert(
            id: id,
            type: type,
            endpoint: endpoint,
            method: method,
            data: data,
            createdAt: createdAt,
            sequenceNumber: sequenceNumber,
            status: status,
            errorMessage: errorMessage,
            retryCount: retryCount,
            rowid: rowid,
          ),
        ));
}

class $$OfflineEventsTableFilterComposer
    extends FilterComposer<_$AppDatabase, $OfflineEventsTable> {
  $$OfflineEventsTableFilterComposer(super.$state);
  ColumnFilters<String> get id => $state.composableBuilder(
      column: $state.table.id,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get type => $state.composableBuilder(
      column: $state.table.type,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get endpoint => $state.composableBuilder(
      column: $state.table.endpoint,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get method => $state.composableBuilder(
      column: $state.table.method,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get data => $state.composableBuilder(
      column: $state.table.data,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<DateTime> get createdAt => $state.composableBuilder(
      column: $state.table.createdAt,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<int> get sequenceNumber => $state.composableBuilder(
      column: $state.table.sequenceNumber,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get status => $state.composableBuilder(
      column: $state.table.status,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get errorMessage => $state.composableBuilder(
      column: $state.table.errorMessage,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<int> get retryCount => $state.composableBuilder(
      column: $state.table.retryCount,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));
}

class $$OfflineEventsTableOrderingComposer
    extends OrderingComposer<_$AppDatabase, $OfflineEventsTable> {
  $$OfflineEventsTableOrderingComposer(super.$state);
  ColumnOrderings<String> get id => $state.composableBuilder(
      column: $state.table.id,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get type => $state.composableBuilder(
      column: $state.table.type,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get endpoint => $state.composableBuilder(
      column: $state.table.endpoint,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get method => $state.composableBuilder(
      column: $state.table.method,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get data => $state.composableBuilder(
      column: $state.table.data,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<DateTime> get createdAt => $state.composableBuilder(
      column: $state.table.createdAt,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<int> get sequenceNumber => $state.composableBuilder(
      column: $state.table.sequenceNumber,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get status => $state.composableBuilder(
      column: $state.table.status,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get errorMessage => $state.composableBuilder(
      column: $state.table.errorMessage,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<int> get retryCount => $state.composableBuilder(
      column: $state.table.retryCount,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));
}

typedef $$CachedContestsTableCreateCompanionBuilder = CachedContestsCompanion
    Function({
  required String id,
  required String tenantId,
  required String competitionId,
  required String name,
  required String status,
  required String sportCode,
  Value<String?> scheduledAt,
  Value<String?> homeParticipantId,
  Value<String?> homeParticipantName,
  Value<String?> awayParticipantId,
  Value<String?> awayParticipantName,
  Value<int?> homeScore,
  Value<int?> awayScore,
  required DateTime cachedAt,
  Value<int> rowid,
});
typedef $$CachedContestsTableUpdateCompanionBuilder = CachedContestsCompanion
    Function({
  Value<String> id,
  Value<String> tenantId,
  Value<String> competitionId,
  Value<String> name,
  Value<String> status,
  Value<String> sportCode,
  Value<String?> scheduledAt,
  Value<String?> homeParticipantId,
  Value<String?> homeParticipantName,
  Value<String?> awayParticipantId,
  Value<String?> awayParticipantName,
  Value<int?> homeScore,
  Value<int?> awayScore,
  Value<DateTime> cachedAt,
  Value<int> rowid,
});

class $$CachedContestsTableTableManager extends RootTableManager<
    _$AppDatabase,
    $CachedContestsTable,
    CachedContest,
    $$CachedContestsTableFilterComposer,
    $$CachedContestsTableOrderingComposer,
    $$CachedContestsTableCreateCompanionBuilder,
    $$CachedContestsTableUpdateCompanionBuilder> {
  $$CachedContestsTableTableManager(
      _$AppDatabase db, $CachedContestsTable table)
      : super(TableManagerState(
          db: db,
          table: table,
          filteringComposer:
              $$CachedContestsTableFilterComposer(ComposerState(db, table)),
          orderingComposer:
              $$CachedContestsTableOrderingComposer(ComposerState(db, table)),
          updateCompanionCallback: ({
            Value<String> id = const Value.absent(),
            Value<String> tenantId = const Value.absent(),
            Value<String> competitionId = const Value.absent(),
            Value<String> name = const Value.absent(),
            Value<String> status = const Value.absent(),
            Value<String> sportCode = const Value.absent(),
            Value<String?> scheduledAt = const Value.absent(),
            Value<String?> homeParticipantId = const Value.absent(),
            Value<String?> homeParticipantName = const Value.absent(),
            Value<String?> awayParticipantId = const Value.absent(),
            Value<String?> awayParticipantName = const Value.absent(),
            Value<int?> homeScore = const Value.absent(),
            Value<int?> awayScore = const Value.absent(),
            Value<DateTime> cachedAt = const Value.absent(),
            Value<int> rowid = const Value.absent(),
          }) =>
              CachedContestsCompanion(
            id: id,
            tenantId: tenantId,
            competitionId: competitionId,
            name: name,
            status: status,
            sportCode: sportCode,
            scheduledAt: scheduledAt,
            homeParticipantId: homeParticipantId,
            homeParticipantName: homeParticipantName,
            awayParticipantId: awayParticipantId,
            awayParticipantName: awayParticipantName,
            homeScore: homeScore,
            awayScore: awayScore,
            cachedAt: cachedAt,
            rowid: rowid,
          ),
          createCompanionCallback: ({
            required String id,
            required String tenantId,
            required String competitionId,
            required String name,
            required String status,
            required String sportCode,
            Value<String?> scheduledAt = const Value.absent(),
            Value<String?> homeParticipantId = const Value.absent(),
            Value<String?> homeParticipantName = const Value.absent(),
            Value<String?> awayParticipantId = const Value.absent(),
            Value<String?> awayParticipantName = const Value.absent(),
            Value<int?> homeScore = const Value.absent(),
            Value<int?> awayScore = const Value.absent(),
            required DateTime cachedAt,
            Value<int> rowid = const Value.absent(),
          }) =>
              CachedContestsCompanion.insert(
            id: id,
            tenantId: tenantId,
            competitionId: competitionId,
            name: name,
            status: status,
            sportCode: sportCode,
            scheduledAt: scheduledAt,
            homeParticipantId: homeParticipantId,
            homeParticipantName: homeParticipantName,
            awayParticipantId: awayParticipantId,
            awayParticipantName: awayParticipantName,
            homeScore: homeScore,
            awayScore: awayScore,
            cachedAt: cachedAt,
            rowid: rowid,
          ),
        ));
}

class $$CachedContestsTableFilterComposer
    extends FilterComposer<_$AppDatabase, $CachedContestsTable> {
  $$CachedContestsTableFilterComposer(super.$state);
  ColumnFilters<String> get id => $state.composableBuilder(
      column: $state.table.id,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get tenantId => $state.composableBuilder(
      column: $state.table.tenantId,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get competitionId => $state.composableBuilder(
      column: $state.table.competitionId,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get name => $state.composableBuilder(
      column: $state.table.name,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get status => $state.composableBuilder(
      column: $state.table.status,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get sportCode => $state.composableBuilder(
      column: $state.table.sportCode,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get scheduledAt => $state.composableBuilder(
      column: $state.table.scheduledAt,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get homeParticipantId => $state.composableBuilder(
      column: $state.table.homeParticipantId,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get homeParticipantName => $state.composableBuilder(
      column: $state.table.homeParticipantName,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get awayParticipantId => $state.composableBuilder(
      column: $state.table.awayParticipantId,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get awayParticipantName => $state.composableBuilder(
      column: $state.table.awayParticipantName,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<int> get homeScore => $state.composableBuilder(
      column: $state.table.homeScore,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<int> get awayScore => $state.composableBuilder(
      column: $state.table.awayScore,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<DateTime> get cachedAt => $state.composableBuilder(
      column: $state.table.cachedAt,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));
}

class $$CachedContestsTableOrderingComposer
    extends OrderingComposer<_$AppDatabase, $CachedContestsTable> {
  $$CachedContestsTableOrderingComposer(super.$state);
  ColumnOrderings<String> get id => $state.composableBuilder(
      column: $state.table.id,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get tenantId => $state.composableBuilder(
      column: $state.table.tenantId,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get competitionId => $state.composableBuilder(
      column: $state.table.competitionId,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get name => $state.composableBuilder(
      column: $state.table.name,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get status => $state.composableBuilder(
      column: $state.table.status,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get sportCode => $state.composableBuilder(
      column: $state.table.sportCode,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get scheduledAt => $state.composableBuilder(
      column: $state.table.scheduledAt,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get homeParticipantId => $state.composableBuilder(
      column: $state.table.homeParticipantId,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get homeParticipantName => $state.composableBuilder(
      column: $state.table.homeParticipantName,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get awayParticipantId => $state.composableBuilder(
      column: $state.table.awayParticipantId,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get awayParticipantName => $state.composableBuilder(
      column: $state.table.awayParticipantName,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<int> get homeScore => $state.composableBuilder(
      column: $state.table.homeScore,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<int> get awayScore => $state.composableBuilder(
      column: $state.table.awayScore,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<DateTime> get cachedAt => $state.composableBuilder(
      column: $state.table.cachedAt,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));
}

typedef $$CachedParticipantsTableCreateCompanionBuilder
    = CachedParticipantsCompanion Function({
  required String id,
  required String tenantId,
  required String name,
  required String type,
  Value<String?> sportCode,
  Value<String?> avatarUrl,
  Value<String?> metadata,
  required DateTime cachedAt,
  Value<int> rowid,
});
typedef $$CachedParticipantsTableUpdateCompanionBuilder
    = CachedParticipantsCompanion Function({
  Value<String> id,
  Value<String> tenantId,
  Value<String> name,
  Value<String> type,
  Value<String?> sportCode,
  Value<String?> avatarUrl,
  Value<String?> metadata,
  Value<DateTime> cachedAt,
  Value<int> rowid,
});

class $$CachedParticipantsTableTableManager extends RootTableManager<
    _$AppDatabase,
    $CachedParticipantsTable,
    CachedParticipant,
    $$CachedParticipantsTableFilterComposer,
    $$CachedParticipantsTableOrderingComposer,
    $$CachedParticipantsTableCreateCompanionBuilder,
    $$CachedParticipantsTableUpdateCompanionBuilder> {
  $$CachedParticipantsTableTableManager(
      _$AppDatabase db, $CachedParticipantsTable table)
      : super(TableManagerState(
          db: db,
          table: table,
          filteringComposer:
              $$CachedParticipantsTableFilterComposer(ComposerState(db, table)),
          orderingComposer: $$CachedParticipantsTableOrderingComposer(
              ComposerState(db, table)),
          updateCompanionCallback: ({
            Value<String> id = const Value.absent(),
            Value<String> tenantId = const Value.absent(),
            Value<String> name = const Value.absent(),
            Value<String> type = const Value.absent(),
            Value<String?> sportCode = const Value.absent(),
            Value<String?> avatarUrl = const Value.absent(),
            Value<String?> metadata = const Value.absent(),
            Value<DateTime> cachedAt = const Value.absent(),
            Value<int> rowid = const Value.absent(),
          }) =>
              CachedParticipantsCompanion(
            id: id,
            tenantId: tenantId,
            name: name,
            type: type,
            sportCode: sportCode,
            avatarUrl: avatarUrl,
            metadata: metadata,
            cachedAt: cachedAt,
            rowid: rowid,
          ),
          createCompanionCallback: ({
            required String id,
            required String tenantId,
            required String name,
            required String type,
            Value<String?> sportCode = const Value.absent(),
            Value<String?> avatarUrl = const Value.absent(),
            Value<String?> metadata = const Value.absent(),
            required DateTime cachedAt,
            Value<int> rowid = const Value.absent(),
          }) =>
              CachedParticipantsCompanion.insert(
            id: id,
            tenantId: tenantId,
            name: name,
            type: type,
            sportCode: sportCode,
            avatarUrl: avatarUrl,
            metadata: metadata,
            cachedAt: cachedAt,
            rowid: rowid,
          ),
        ));
}

class $$CachedParticipantsTableFilterComposer
    extends FilterComposer<_$AppDatabase, $CachedParticipantsTable> {
  $$CachedParticipantsTableFilterComposer(super.$state);
  ColumnFilters<String> get id => $state.composableBuilder(
      column: $state.table.id,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get tenantId => $state.composableBuilder(
      column: $state.table.tenantId,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get name => $state.composableBuilder(
      column: $state.table.name,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get type => $state.composableBuilder(
      column: $state.table.type,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get sportCode => $state.composableBuilder(
      column: $state.table.sportCode,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get avatarUrl => $state.composableBuilder(
      column: $state.table.avatarUrl,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get metadata => $state.composableBuilder(
      column: $state.table.metadata,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<DateTime> get cachedAt => $state.composableBuilder(
      column: $state.table.cachedAt,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));
}

class $$CachedParticipantsTableOrderingComposer
    extends OrderingComposer<_$AppDatabase, $CachedParticipantsTable> {
  $$CachedParticipantsTableOrderingComposer(super.$state);
  ColumnOrderings<String> get id => $state.composableBuilder(
      column: $state.table.id,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get tenantId => $state.composableBuilder(
      column: $state.table.tenantId,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get name => $state.composableBuilder(
      column: $state.table.name,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get type => $state.composableBuilder(
      column: $state.table.type,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get sportCode => $state.composableBuilder(
      column: $state.table.sportCode,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get avatarUrl => $state.composableBuilder(
      column: $state.table.avatarUrl,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get metadata => $state.composableBuilder(
      column: $state.table.metadata,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<DateTime> get cachedAt => $state.composableBuilder(
      column: $state.table.cachedAt,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));
}

typedef $$CachedCompetitionsTableCreateCompanionBuilder
    = CachedCompetitionsCompanion Function({
  required String id,
  required String tenantId,
  required String name,
  required String sportCode,
  required String status,
  Value<String?> format,
  required DateTime cachedAt,
  Value<int> rowid,
});
typedef $$CachedCompetitionsTableUpdateCompanionBuilder
    = CachedCompetitionsCompanion Function({
  Value<String> id,
  Value<String> tenantId,
  Value<String> name,
  Value<String> sportCode,
  Value<String> status,
  Value<String?> format,
  Value<DateTime> cachedAt,
  Value<int> rowid,
});

class $$CachedCompetitionsTableTableManager extends RootTableManager<
    _$AppDatabase,
    $CachedCompetitionsTable,
    CachedCompetition,
    $$CachedCompetitionsTableFilterComposer,
    $$CachedCompetitionsTableOrderingComposer,
    $$CachedCompetitionsTableCreateCompanionBuilder,
    $$CachedCompetitionsTableUpdateCompanionBuilder> {
  $$CachedCompetitionsTableTableManager(
      _$AppDatabase db, $CachedCompetitionsTable table)
      : super(TableManagerState(
          db: db,
          table: table,
          filteringComposer:
              $$CachedCompetitionsTableFilterComposer(ComposerState(db, table)),
          orderingComposer: $$CachedCompetitionsTableOrderingComposer(
              ComposerState(db, table)),
          updateCompanionCallback: ({
            Value<String> id = const Value.absent(),
            Value<String> tenantId = const Value.absent(),
            Value<String> name = const Value.absent(),
            Value<String> sportCode = const Value.absent(),
            Value<String> status = const Value.absent(),
            Value<String?> format = const Value.absent(),
            Value<DateTime> cachedAt = const Value.absent(),
            Value<int> rowid = const Value.absent(),
          }) =>
              CachedCompetitionsCompanion(
            id: id,
            tenantId: tenantId,
            name: name,
            sportCode: sportCode,
            status: status,
            format: format,
            cachedAt: cachedAt,
            rowid: rowid,
          ),
          createCompanionCallback: ({
            required String id,
            required String tenantId,
            required String name,
            required String sportCode,
            required String status,
            Value<String?> format = const Value.absent(),
            required DateTime cachedAt,
            Value<int> rowid = const Value.absent(),
          }) =>
              CachedCompetitionsCompanion.insert(
            id: id,
            tenantId: tenantId,
            name: name,
            sportCode: sportCode,
            status: status,
            format: format,
            cachedAt: cachedAt,
            rowid: rowid,
          ),
        ));
}

class $$CachedCompetitionsTableFilterComposer
    extends FilterComposer<_$AppDatabase, $CachedCompetitionsTable> {
  $$CachedCompetitionsTableFilterComposer(super.$state);
  ColumnFilters<String> get id => $state.composableBuilder(
      column: $state.table.id,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get tenantId => $state.composableBuilder(
      column: $state.table.tenantId,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get name => $state.composableBuilder(
      column: $state.table.name,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get sportCode => $state.composableBuilder(
      column: $state.table.sportCode,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get status => $state.composableBuilder(
      column: $state.table.status,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get format => $state.composableBuilder(
      column: $state.table.format,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<DateTime> get cachedAt => $state.composableBuilder(
      column: $state.table.cachedAt,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));
}

class $$CachedCompetitionsTableOrderingComposer
    extends OrderingComposer<_$AppDatabase, $CachedCompetitionsTable> {
  $$CachedCompetitionsTableOrderingComposer(super.$state);
  ColumnOrderings<String> get id => $state.composableBuilder(
      column: $state.table.id,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get tenantId => $state.composableBuilder(
      column: $state.table.tenantId,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get name => $state.composableBuilder(
      column: $state.table.name,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get sportCode => $state.composableBuilder(
      column: $state.table.sportCode,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get status => $state.composableBuilder(
      column: $state.table.status,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get format => $state.composableBuilder(
      column: $state.table.format,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<DateTime> get cachedAt => $state.composableBuilder(
      column: $state.table.cachedAt,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));
}

typedef $$CachedSupporterResponsesTableCreateCompanionBuilder
    = CachedSupporterResponsesCompanion Function({
  required String key,
  required String tenantId,
  required String kind,
  required String data,
  required DateTime cachedAt,
  Value<int> rowid,
});
typedef $$CachedSupporterResponsesTableUpdateCompanionBuilder
    = CachedSupporterResponsesCompanion Function({
  Value<String> key,
  Value<String> tenantId,
  Value<String> kind,
  Value<String> data,
  Value<DateTime> cachedAt,
  Value<int> rowid,
});

class $$CachedSupporterResponsesTableTableManager extends RootTableManager<
    _$AppDatabase,
    $CachedSupporterResponsesTable,
    CachedSupporterResponse,
    $$CachedSupporterResponsesTableFilterComposer,
    $$CachedSupporterResponsesTableOrderingComposer,
    $$CachedSupporterResponsesTableCreateCompanionBuilder,
    $$CachedSupporterResponsesTableUpdateCompanionBuilder> {
  $$CachedSupporterResponsesTableTableManager(
      _$AppDatabase db, $CachedSupporterResponsesTable table)
      : super(TableManagerState(
          db: db,
          table: table,
          filteringComposer: $$CachedSupporterResponsesTableFilterComposer(
              ComposerState(db, table)),
          orderingComposer: $$CachedSupporterResponsesTableOrderingComposer(
              ComposerState(db, table)),
          updateCompanionCallback: ({
            Value<String> key = const Value.absent(),
            Value<String> tenantId = const Value.absent(),
            Value<String> kind = const Value.absent(),
            Value<String> data = const Value.absent(),
            Value<DateTime> cachedAt = const Value.absent(),
            Value<int> rowid = const Value.absent(),
          }) =>
              CachedSupporterResponsesCompanion(
            key: key,
            tenantId: tenantId,
            kind: kind,
            data: data,
            cachedAt: cachedAt,
            rowid: rowid,
          ),
          createCompanionCallback: ({
            required String key,
            required String tenantId,
            required String kind,
            required String data,
            required DateTime cachedAt,
            Value<int> rowid = const Value.absent(),
          }) =>
              CachedSupporterResponsesCompanion.insert(
            key: key,
            tenantId: tenantId,
            kind: kind,
            data: data,
            cachedAt: cachedAt,
            rowid: rowid,
          ),
        ));
}

class $$CachedSupporterResponsesTableFilterComposer
    extends FilterComposer<_$AppDatabase, $CachedSupporterResponsesTable> {
  $$CachedSupporterResponsesTableFilterComposer(super.$state);
  ColumnFilters<String> get key => $state.composableBuilder(
      column: $state.table.key,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get tenantId => $state.composableBuilder(
      column: $state.table.tenantId,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get kind => $state.composableBuilder(
      column: $state.table.kind,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get data => $state.composableBuilder(
      column: $state.table.data,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<DateTime> get cachedAt => $state.composableBuilder(
      column: $state.table.cachedAt,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));
}

class $$CachedSupporterResponsesTableOrderingComposer
    extends OrderingComposer<_$AppDatabase, $CachedSupporterResponsesTable> {
  $$CachedSupporterResponsesTableOrderingComposer(super.$state);
  ColumnOrderings<String> get key => $state.composableBuilder(
      column: $state.table.key,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get tenantId => $state.composableBuilder(
      column: $state.table.tenantId,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get kind => $state.composableBuilder(
      column: $state.table.kind,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get data => $state.composableBuilder(
      column: $state.table.data,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<DateTime> get cachedAt => $state.composableBuilder(
      column: $state.table.cachedAt,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));
}

typedef $$ScoringSessionsTableCreateCompanionBuilder = ScoringSessionsCompanion
    Function({
  required String id,
  required String contestId,
  required String userId,
  required String tenantId,
  required String sportCode,
  Value<String> status,
  required DateTime startedAt,
  required DateTime lastUpdatedAt,
  required String currentState,
  Value<int> syncedEventsCount,
  Value<int> rowid,
});
typedef $$ScoringSessionsTableUpdateCompanionBuilder = ScoringSessionsCompanion
    Function({
  Value<String> id,
  Value<String> contestId,
  Value<String> userId,
  Value<String> tenantId,
  Value<String> sportCode,
  Value<String> status,
  Value<DateTime> startedAt,
  Value<DateTime> lastUpdatedAt,
  Value<String> currentState,
  Value<int> syncedEventsCount,
  Value<int> rowid,
});

class $$ScoringSessionsTableTableManager extends RootTableManager<
    _$AppDatabase,
    $ScoringSessionsTable,
    ScoringSession,
    $$ScoringSessionsTableFilterComposer,
    $$ScoringSessionsTableOrderingComposer,
    $$ScoringSessionsTableCreateCompanionBuilder,
    $$ScoringSessionsTableUpdateCompanionBuilder> {
  $$ScoringSessionsTableTableManager(
      _$AppDatabase db, $ScoringSessionsTable table)
      : super(TableManagerState(
          db: db,
          table: table,
          filteringComposer:
              $$ScoringSessionsTableFilterComposer(ComposerState(db, table)),
          orderingComposer:
              $$ScoringSessionsTableOrderingComposer(ComposerState(db, table)),
          updateCompanionCallback: ({
            Value<String> id = const Value.absent(),
            Value<String> contestId = const Value.absent(),
            Value<String> userId = const Value.absent(),
            Value<String> tenantId = const Value.absent(),
            Value<String> sportCode = const Value.absent(),
            Value<String> status = const Value.absent(),
            Value<DateTime> startedAt = const Value.absent(),
            Value<DateTime> lastUpdatedAt = const Value.absent(),
            Value<String> currentState = const Value.absent(),
            Value<int> syncedEventsCount = const Value.absent(),
            Value<int> rowid = const Value.absent(),
          }) =>
              ScoringSessionsCompanion(
            id: id,
            contestId: contestId,
            userId: userId,
            tenantId: tenantId,
            sportCode: sportCode,
            status: status,
            startedAt: startedAt,
            lastUpdatedAt: lastUpdatedAt,
            currentState: currentState,
            syncedEventsCount: syncedEventsCount,
            rowid: rowid,
          ),
          createCompanionCallback: ({
            required String id,
            required String contestId,
            required String userId,
            required String tenantId,
            required String sportCode,
            Value<String> status = const Value.absent(),
            required DateTime startedAt,
            required DateTime lastUpdatedAt,
            required String currentState,
            Value<int> syncedEventsCount = const Value.absent(),
            Value<int> rowid = const Value.absent(),
          }) =>
              ScoringSessionsCompanion.insert(
            id: id,
            contestId: contestId,
            userId: userId,
            tenantId: tenantId,
            sportCode: sportCode,
            status: status,
            startedAt: startedAt,
            lastUpdatedAt: lastUpdatedAt,
            currentState: currentState,
            syncedEventsCount: syncedEventsCount,
            rowid: rowid,
          ),
        ));
}

class $$ScoringSessionsTableFilterComposer
    extends FilterComposer<_$AppDatabase, $ScoringSessionsTable> {
  $$ScoringSessionsTableFilterComposer(super.$state);
  ColumnFilters<String> get id => $state.composableBuilder(
      column: $state.table.id,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get contestId => $state.composableBuilder(
      column: $state.table.contestId,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get userId => $state.composableBuilder(
      column: $state.table.userId,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get tenantId => $state.composableBuilder(
      column: $state.table.tenantId,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get sportCode => $state.composableBuilder(
      column: $state.table.sportCode,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get status => $state.composableBuilder(
      column: $state.table.status,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<DateTime> get startedAt => $state.composableBuilder(
      column: $state.table.startedAt,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<DateTime> get lastUpdatedAt => $state.composableBuilder(
      column: $state.table.lastUpdatedAt,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get currentState => $state.composableBuilder(
      column: $state.table.currentState,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<int> get syncedEventsCount => $state.composableBuilder(
      column: $state.table.syncedEventsCount,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));
}

class $$ScoringSessionsTableOrderingComposer
    extends OrderingComposer<_$AppDatabase, $ScoringSessionsTable> {
  $$ScoringSessionsTableOrderingComposer(super.$state);
  ColumnOrderings<String> get id => $state.composableBuilder(
      column: $state.table.id,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get contestId => $state.composableBuilder(
      column: $state.table.contestId,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get userId => $state.composableBuilder(
      column: $state.table.userId,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get tenantId => $state.composableBuilder(
      column: $state.table.tenantId,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get sportCode => $state.composableBuilder(
      column: $state.table.sportCode,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get status => $state.composableBuilder(
      column: $state.table.status,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<DateTime> get startedAt => $state.composableBuilder(
      column: $state.table.startedAt,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<DateTime> get lastUpdatedAt => $state.composableBuilder(
      column: $state.table.lastUpdatedAt,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get currentState => $state.composableBuilder(
      column: $state.table.currentState,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<int> get syncedEventsCount => $state.composableBuilder(
      column: $state.table.syncedEventsCount,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));
}

typedef $$UserSessionsTableCreateCompanionBuilder = UserSessionsCompanion
    Function({
  required String id,
  required String userId,
  required String tenantId,
  required String email,
  required String displayName,
  required String role,
  required String accessToken,
  Value<String?> refreshToken,
  required DateTime expiresAt,
  required DateTime createdAt,
  Value<int> rowid,
});
typedef $$UserSessionsTableUpdateCompanionBuilder = UserSessionsCompanion
    Function({
  Value<String> id,
  Value<String> userId,
  Value<String> tenantId,
  Value<String> email,
  Value<String> displayName,
  Value<String> role,
  Value<String> accessToken,
  Value<String?> refreshToken,
  Value<DateTime> expiresAt,
  Value<DateTime> createdAt,
  Value<int> rowid,
});

class $$UserSessionsTableTableManager extends RootTableManager<
    _$AppDatabase,
    $UserSessionsTable,
    UserSession,
    $$UserSessionsTableFilterComposer,
    $$UserSessionsTableOrderingComposer,
    $$UserSessionsTableCreateCompanionBuilder,
    $$UserSessionsTableUpdateCompanionBuilder> {
  $$UserSessionsTableTableManager(_$AppDatabase db, $UserSessionsTable table)
      : super(TableManagerState(
          db: db,
          table: table,
          filteringComposer:
              $$UserSessionsTableFilterComposer(ComposerState(db, table)),
          orderingComposer:
              $$UserSessionsTableOrderingComposer(ComposerState(db, table)),
          updateCompanionCallback: ({
            Value<String> id = const Value.absent(),
            Value<String> userId = const Value.absent(),
            Value<String> tenantId = const Value.absent(),
            Value<String> email = const Value.absent(),
            Value<String> displayName = const Value.absent(),
            Value<String> role = const Value.absent(),
            Value<String> accessToken = const Value.absent(),
            Value<String?> refreshToken = const Value.absent(),
            Value<DateTime> expiresAt = const Value.absent(),
            Value<DateTime> createdAt = const Value.absent(),
            Value<int> rowid = const Value.absent(),
          }) =>
              UserSessionsCompanion(
            id: id,
            userId: userId,
            tenantId: tenantId,
            email: email,
            displayName: displayName,
            role: role,
            accessToken: accessToken,
            refreshToken: refreshToken,
            expiresAt: expiresAt,
            createdAt: createdAt,
            rowid: rowid,
          ),
          createCompanionCallback: ({
            required String id,
            required String userId,
            required String tenantId,
            required String email,
            required String displayName,
            required String role,
            required String accessToken,
            Value<String?> refreshToken = const Value.absent(),
            required DateTime expiresAt,
            required DateTime createdAt,
            Value<int> rowid = const Value.absent(),
          }) =>
              UserSessionsCompanion.insert(
            id: id,
            userId: userId,
            tenantId: tenantId,
            email: email,
            displayName: displayName,
            role: role,
            accessToken: accessToken,
            refreshToken: refreshToken,
            expiresAt: expiresAt,
            createdAt: createdAt,
            rowid: rowid,
          ),
        ));
}

class $$UserSessionsTableFilterComposer
    extends FilterComposer<_$AppDatabase, $UserSessionsTable> {
  $$UserSessionsTableFilterComposer(super.$state);
  ColumnFilters<String> get id => $state.composableBuilder(
      column: $state.table.id,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get userId => $state.composableBuilder(
      column: $state.table.userId,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get tenantId => $state.composableBuilder(
      column: $state.table.tenantId,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get email => $state.composableBuilder(
      column: $state.table.email,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get displayName => $state.composableBuilder(
      column: $state.table.displayName,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get role => $state.composableBuilder(
      column: $state.table.role,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get accessToken => $state.composableBuilder(
      column: $state.table.accessToken,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<String> get refreshToken => $state.composableBuilder(
      column: $state.table.refreshToken,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<DateTime> get expiresAt => $state.composableBuilder(
      column: $state.table.expiresAt,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));

  ColumnFilters<DateTime> get createdAt => $state.composableBuilder(
      column: $state.table.createdAt,
      builder: (column, joinBuilders) =>
          ColumnFilters(column, joinBuilders: joinBuilders));
}

class $$UserSessionsTableOrderingComposer
    extends OrderingComposer<_$AppDatabase, $UserSessionsTable> {
  $$UserSessionsTableOrderingComposer(super.$state);
  ColumnOrderings<String> get id => $state.composableBuilder(
      column: $state.table.id,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get userId => $state.composableBuilder(
      column: $state.table.userId,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get tenantId => $state.composableBuilder(
      column: $state.table.tenantId,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get email => $state.composableBuilder(
      column: $state.table.email,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get displayName => $state.composableBuilder(
      column: $state.table.displayName,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get role => $state.composableBuilder(
      column: $state.table.role,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get accessToken => $state.composableBuilder(
      column: $state.table.accessToken,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<String> get refreshToken => $state.composableBuilder(
      column: $state.table.refreshToken,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<DateTime> get expiresAt => $state.composableBuilder(
      column: $state.table.expiresAt,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));

  ColumnOrderings<DateTime> get createdAt => $state.composableBuilder(
      column: $state.table.createdAt,
      builder: (column, joinBuilders) =>
          ColumnOrderings(column, joinBuilders: joinBuilders));
}

class $AppDatabaseManager {
  final _$AppDatabase _db;
  $AppDatabaseManager(this._db);
  $$OfflineEventsTableTableManager get offlineEvents =>
      $$OfflineEventsTableTableManager(_db, _db.offlineEvents);
  $$CachedContestsTableTableManager get cachedContests =>
      $$CachedContestsTableTableManager(_db, _db.cachedContests);
  $$CachedParticipantsTableTableManager get cachedParticipants =>
      $$CachedParticipantsTableTableManager(_db, _db.cachedParticipants);
  $$CachedCompetitionsTableTableManager get cachedCompetitions =>
      $$CachedCompetitionsTableTableManager(_db, _db.cachedCompetitions);
  $$CachedSupporterResponsesTableTableManager get cachedSupporterResponses =>
      $$CachedSupporterResponsesTableTableManager(
          _db, _db.cachedSupporterResponses);
  $$ScoringSessionsTableTableManager get scoringSessions =>
      $$ScoringSessionsTableTableManager(_db, _db.scoringSessions);
  $$UserSessionsTableTableManager get userSessions =>
      $$UserSessionsTableTableManager(_db, _db.userSessions);
}
