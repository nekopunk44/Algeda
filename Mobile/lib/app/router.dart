import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/auth/app_role.dart';
import '../core/auth/auth_controller.dart';
import '../features/admin/presentation/screens/admin_more_screen.dart';
import '../features/admin_management/presentation/screens/admin_access_screen.dart';
import '../features/admin_management/presentation/screens/admin_clients_screen.dart';
import '../features/admin_management/presentation/screens/admin_criteria_screen.dart';
import '../features/admin_management/presentation/screens/admin_currencies_screen.dart';
import '../features/analytics/presentation/screens/admin_analytics_screen.dart';
import '../features/auth/presentation/screens/login_screen.dart';
import '../features/complaints/presentation/screens/admin_complaint_details_screen.dart';
import '../features/chats/presentation/screens/admin_chats_screen.dart';
import '../features/chats/presentation/screens/deal_chat_screen.dart';
import '../features/chats/presentation/screens/realtor_chats_screen.dart';
import '../features/complaints/presentation/screens/admin_complaints_screen.dart';
import '../features/complaints/presentation/screens/realtor_complaints_screen.dart';
import '../features/dashboard/presentation/screens/admin_dashboard_screen.dart';
import '../features/dashboard/presentation/screens/realtor_dashboard_screen.dart';
import '../features/deals/presentation/screens/admin_deals_screen.dart';
import '../features/deals/presentation/screens/admin_document_access_screen.dart';
import '../features/deals/presentation/screens/deal_details_screen.dart';
import '../features/deals/presentation/screens/realtor_deals_screen.dart';
import '../features/matching/presentation/screens/matching_screen.dart';
import '../features/profile/presentation/screens/profile_screen.dart';
import '../features/profile/presentation/screens/realtor_more_screen.dart';
import '../features/properties/presentation/screens/properties_screen.dart';
import '../features/properties/presentation/screens/property_details_screen.dart';
import '../features/properties/presentation/screens/property_form_screen.dart';
import '../features/realtor_efficiency/presentation/screens/efficiency_screen.dart';
import '../features/realtor_efficiency/presentation/screens/eligibility_settings_screen.dart';
import 'role_shell.dart';

final appRouterProvider = Provider<GoRouter>((ref) {
  var authState = ref.read(authControllerProvider);
  final refreshNotifier = _RouterRefreshNotifier();

  ref.listen<AuthState>(authControllerProvider, (_, next) {
    authState = next;
    refreshNotifier.notify();
  });

  final router = GoRouter(
    initialLocation: '/splash',
    refreshListenable: refreshNotifier,
    redirect: (context, state) {
      final path = state.uri.path;
      final isAuthRoute = path == '/auth';
      final isSplashRoute = path == '/splash';

      if (authState.isLoading) {
        return isSplashRoute ? null : '/splash';
      }

      if (!authState.isAuthenticated) {
        return isAuthRoute ? null : '/auth';
      }

      final role = authState.role;
      if (role == null) {
        return '/auth';
      }

      if (path == '/' || isAuthRoute || isSplashRoute) {
        return role.homePath;
      }

      if (!role.ownsPath(path)) {
        return role.homePath;
      }

      return null;
    },
    routes: [
      GoRoute(
        path: '/splash',
        builder: (context, state) => const _StartupScreen(),
      ),
      GoRoute(path: '/auth', builder: (context, state) => const LoginScreen()),
      ShellRoute(
        builder: (context, state, child) {
          return RoleShell(role: AppRole.realtor, child: child);
        },
        routes: [
          GoRoute(
            path: '/realtor',
            builder: (context, state) => const RealtorDashboardScreen(),
          ),
          GoRoute(
            path: '/realtor/deals',
            builder: (context, state) => const RealtorDealsScreen(),
          ),
          GoRoute(
            path: '/realtor/deals/:id',
            builder: (context, state) => DealDetailsScreen(
              dealId: state.pathParameters['id'] ?? '',
              role: AppRole.realtor,
            ),
          ),
          GoRoute(
            path: '/realtor/properties',
            builder: (context, state) =>
                const PropertiesScreen(role: AppRole.realtor),
          ),
          GoRoute(
            path: '/realtor/properties/new',
            builder: (context, state) =>
                const PropertyFormScreen(role: AppRole.realtor),
          ),
          GoRoute(
            path: '/realtor/properties/:id',
            builder: (context, state) => PropertyDetailsScreen(
              propertyId: state.pathParameters['id'] ?? '',
              role: AppRole.realtor,
            ),
          ),
          GoRoute(
            path: '/realtor/properties/:id/edit',
            builder: (context, state) => PropertyFormScreen(
              propertyId: state.pathParameters['id'] ?? '',
              role: AppRole.realtor,
            ),
          ),
          GoRoute(
            path: '/realtor/chats',
            builder: (context, state) => const RealtorChatsScreen(),
          ),
          GoRoute(
            path: '/realtor/chats/:dealId',
            builder: (context, state) => DealChatScreen(
              dealId: state.pathParameters['dealId'] ?? '',
              role: AppRole.realtor,
            ),
          ),
          GoRoute(
            path: '/realtor/more',
            builder: (context, state) => const RealtorMoreScreen(),
          ),
          GoRoute(
            path: '/realtor/efficiency',
            builder: (context, state) =>
                const EfficiencyScreen(role: AppRole.realtor),
          ),
          GoRoute(
            path: '/realtor/complaints',
            builder: (context, state) => const RealtorComplaintsScreen(),
          ),
          GoRoute(
            path: '/realtor/matching',
            builder: (context, state) =>
                const MatchingScreen(role: AppRole.realtor),
          ),
          GoRoute(
            path: '/realtor/profile',
            builder: (context, state) => const ProfileScreen(),
          ),
        ],
      ),
      ShellRoute(
        builder: (context, state, child) {
          return RoleShell(role: AppRole.admin, child: child);
        },
        routes: [
          GoRoute(
            path: '/admin',
            builder: (context, state) => const AdminDashboardScreen(),
          ),
          GoRoute(
            path: '/admin/deals',
            builder: (context, state) => const AdminDealsScreen(),
          ),
          GoRoute(
            path: '/admin/deals/:id',
            builder: (context, state) => DealDetailsScreen(
              dealId: state.pathParameters['id'] ?? '',
              role: AppRole.admin,
            ),
          ),
          GoRoute(
            path: '/admin/complaints',
            builder: (context, state) => const AdminComplaintsScreen(),
          ),
          GoRoute(
            path: '/admin/complaints/:id',
            builder: (context, state) => AdminComplaintDetailsScreen(
              complaintId: state.pathParameters['id'] ?? '',
            ),
          ),
          GoRoute(
            path: '/admin/chats',
            builder: (context, state) => const AdminChatsScreen(),
          ),
          GoRoute(
            path: '/admin/chats/:dealId',
            builder: (context, state) => DealChatScreen(
              dealId: state.pathParameters['dealId'] ?? '',
              role: AppRole.admin,
            ),
          ),
          GoRoute(
            path: '/admin/more',
            builder: (context, state) => const AdminMoreScreen(),
          ),
          GoRoute(
            path: '/admin/analytics',
            builder: (context, state) => const AdminAnalyticsScreen(),
          ),
          GoRoute(
            path: '/admin/realtor-efficiency',
            builder: (context, state) =>
                const EfficiencyScreen(role: AppRole.admin),
          ),
          GoRoute(
            path: '/admin/system-settings',
            builder: (context, state) => const SystemSettingsScreen(),
          ),
          GoRoute(
            path: '/admin/eligibility-settings',
            builder: (context, state) => const SystemSettingsScreen(),
          ),
          GoRoute(
            path: '/admin/properties',
            builder: (context, state) =>
                const PropertiesScreen(role: AppRole.admin),
          ),
          GoRoute(
            path: '/admin/properties/new',
            builder: (context, state) =>
                const PropertyFormScreen(role: AppRole.admin),
          ),
          GoRoute(
            path: '/admin/properties/:id',
            builder: (context, state) => PropertyDetailsScreen(
              propertyId: state.pathParameters['id'] ?? '',
              role: AppRole.admin,
            ),
          ),
          GoRoute(
            path: '/admin/properties/:id/edit',
            builder: (context, state) => PropertyFormScreen(
              propertyId: state.pathParameters['id'] ?? '',
              role: AppRole.admin,
            ),
          ),
          GoRoute(
            path: '/admin/matching',
            builder: (context, state) =>
                const MatchingScreen(role: AppRole.admin),
          ),
          GoRoute(
            path: '/admin/property-criteria',
            builder: (context, state) => const AdminCriteriaScreen(),
          ),
          GoRoute(
            path: '/admin/currencies',
            builder: (context, state) => const AdminCurrenciesScreen(),
          ),
          GoRoute(
            path: '/admin/clients',
            builder: (context, state) => const AdminClientsScreen(),
          ),
          GoRoute(
            path: '/admin/access',
            builder: (context, state) => const AdminAccessScreen(),
          ),
          GoRoute(
            path: '/admin/document-access',
            builder: (context, state) => const AdminDocumentAccessScreen(),
          ),
          GoRoute(
            path: '/admin/profile',
            builder: (context, state) => const ProfileScreen(),
          ),
        ],
      ),
    ],
  );

  ref.onDispose(refreshNotifier.dispose);
  ref.onDispose(router.dispose);
  return router;
});

class _RouterRefreshNotifier extends ChangeNotifier {
  void notify() {
    notifyListeners();
  }
}

class _StartupScreen extends StatelessWidget {
  const _StartupScreen();

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      body: SafeArea(
        child: Center(
          child: SizedBox.square(
            dimension: 32,
            child: CircularProgressIndicator(strokeWidth: 2.5),
          ),
        ),
      ),
    );
  }
}
