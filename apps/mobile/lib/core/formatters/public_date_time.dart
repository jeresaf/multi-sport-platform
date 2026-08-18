import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

String formatPublicDateTime(DateTime? value, {DateTime? now}) {
  if (value == null) return 'TBC';
  final local = value.toLocal();
  final today = DateUtils.dateOnly((now ?? DateTime.now()).toLocal());
  final date = DateUtils.dateOnly(local);
  final relative = date == today
      ? 'Today'
      : date == today.add(const Duration(days: 1))
          ? 'Tomorrow'
          : date == today.subtract(const Duration(days: 1))
              ? 'Yesterday'
              : DateFormat('EEE, d MMM').format(local);
  return '$relative · ${DateFormat.Hm().format(local)}';
}

String formatPublicDate(DateTime? value, {DateTime? now}) {
  if (value == null) return 'TBC';
  final local = value.toLocal();
  final today = DateUtils.dateOnly((now ?? DateTime.now()).toLocal());
  final date = DateUtils.dateOnly(local);
  if (date == today) return 'Today';
  if (date == today.add(const Duration(days: 1))) return 'Tomorrow';
  if (date == today.subtract(const Duration(days: 1))) return 'Yesterday';
  return DateFormat('EEE, d MMM yyyy').format(local);
}
