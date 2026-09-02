import 'package:flutter/material.dart';

class AppTheme {
  const AppTheme._();

  static ThemeData get light {
    const primary = Color(0xFF0D6EFD);
    const brandBg = Color(0xFFF3F8FF);
    const scaffold = Color(0xFFF7F9FC);
    const border = Color(0xFFDCE8FF);
    const text = Color(0xFF1F2937);
    const muted = Color(0xFF6C757D);

    final scheme =
        ColorScheme.fromSeed(
          seedColor: primary,
          brightness: Brightness.light,
        ).copyWith(
          primary: primary,
          surface: Colors.white,
          surfaceContainerHighest: brandBg,
          outlineVariant: border,
          onSurface: text,
          onSurfaceVariant: muted,
          primaryContainer: const Color(0xFFCFE2FF),
          onPrimaryContainer: text,
          secondaryContainer: brandBg,
          onSecondaryContainer: text,
        );

    final base = ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: scaffold,
      visualDensity: VisualDensity.compact,
      fontFamily: 'Roboto',
    );

    return base.copyWith(
      textTheme: base.textTheme.copyWith(
        headlineSmall: base.textTheme.headlineSmall?.copyWith(
          fontSize: 21,
          fontWeight: FontWeight.w700,
          height: 1.18,
          color: text,
        ),
        titleLarge: base.textTheme.titleLarge?.copyWith(
          fontSize: 20,
          fontWeight: FontWeight.w700,
          height: 1.2,
          color: text,
        ),
        titleMedium: base.textTheme.titleMedium?.copyWith(
          fontSize: 16,
          fontWeight: FontWeight.w700,
          height: 1.22,
          color: text,
        ),
        titleSmall: base.textTheme.titleSmall?.copyWith(
          fontSize: 14,
          fontWeight: FontWeight.w700,
          height: 1.25,
          color: text,
        ),
        bodyLarge: base.textTheme.bodyLarge?.copyWith(
          fontSize: 15,
          color: text,
        ),
        bodyMedium: base.textTheme.bodyMedium?.copyWith(
          fontSize: 14,
          height: 1.28,
          color: text,
        ),
        bodySmall: base.textTheme.bodySmall?.copyWith(
          fontSize: 12,
          height: 1.25,
          color: muted,
        ),
        labelLarge: base.textTheme.labelLarge?.copyWith(
          fontSize: 14,
          fontWeight: FontWeight.w600,
          color: text,
        ),
        labelMedium: base.textTheme.labelMedium?.copyWith(
          fontSize: 12,
          fontWeight: FontWeight.w600,
          color: text,
        ),
        labelSmall: base.textTheme.labelSmall?.copyWith(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: text,
        ),
      ),
      appBarTheme: const AppBarTheme(
        centerTitle: false,
        elevation: 0,
        scrolledUnderElevation: 1,
        backgroundColor: Colors.white,
        foregroundColor: Color(0xFF1F2937),
        titleTextStyle: TextStyle(
          color: Color(0xFF1F2937),
          fontSize: 16,
          fontWeight: FontWeight.w700,
        ),
      ),
      cardTheme: const CardThemeData(
        elevation: 0,
        margin: EdgeInsets.zero,
        color: Colors.white,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          side: BorderSide(color: border),
          borderRadius: BorderRadius.all(Radius.circular(8)),
        ),
      ),
      inputDecorationTheme: const InputDecorationTheme(
        isDense: true,
        filled: true,
        fillColor: Colors.white,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.all(Radius.circular(8)),
        ),
        enabledBorder: OutlineInputBorder(
          borderSide: BorderSide(color: border),
          borderRadius: BorderRadius.all(Radius.circular(8)),
        ),
      ),
      chipTheme: base.chipTheme.copyWith(
        backgroundColor: brandBg,
        selectedColor: const Color(0xFFCFE2FF),
        side: const BorderSide(color: border),
        labelStyle: const TextStyle(fontSize: 12, height: 1.15, color: text),
        secondaryLabelStyle: const TextStyle(
          fontSize: 12,
          height: 1.15,
          color: text,
          fontWeight: FontWeight.w600,
        ),
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 0),
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.all(Radius.circular(8)),
        ),
      ),
      segmentedButtonTheme: SegmentedButtonThemeData(
        style: ButtonStyle(
          foregroundColor: WidgetStateProperty.resolveWith((states) {
            return states.contains(WidgetState.selected) ? primary : text;
          }),
          backgroundColor: WidgetStateProperty.resolveWith((states) {
            return states.contains(WidgetState.selected)
                ? const Color(0xFFCFE2FF)
                : Colors.white;
          }),
          side: const WidgetStatePropertyAll(BorderSide(color: border)),
          textStyle: const WidgetStatePropertyAll(
            TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
          ),
        ),
      ),
      badgeTheme: const BadgeThemeData(
        backgroundColor: Color(0xFFDC3545),
        textColor: Colors.white,
      ),
      dividerTheme: const DividerThemeData(color: border, space: 1),
      bottomSheetTheme: const BottomSheetThemeData(
        backgroundColor: Colors.white,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.zero),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          textStyle: const TextStyle(fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: primary,
          side: const BorderSide(color: border),
          textStyle: const TextStyle(fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        ),
      ),
      listTileTheme: const ListTileThemeData(
        dense: true,
        minLeadingWidth: 28,
        contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 2),
        iconColor: text,
        textColor: text,
      ),
      iconTheme: const IconThemeData(size: 22, color: text),
      floatingActionButtonTheme: const FloatingActionButtonThemeData(
        backgroundColor: primary,
        foregroundColor: Colors.white,
      ),
      navigationBarTheme: const NavigationBarThemeData(
        height: 66,
        backgroundColor: Colors.white,
        surfaceTintColor: Colors.transparent,
        indicatorColor: Color(0xFFCFE2FF),
        labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
        labelTextStyle: WidgetStatePropertyAll(
          TextStyle(fontSize: 10.5, fontWeight: FontWeight.w600),
        ),
        iconTheme: WidgetStatePropertyAll(IconThemeData(size: 22)),
      ),
    );
  }
}
