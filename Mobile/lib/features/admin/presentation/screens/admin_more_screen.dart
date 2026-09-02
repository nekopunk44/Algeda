import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/auth/auth_controller.dart';

class AdminMoreScreen extends ConsumerWidget {
  const AdminMoreScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('Еще', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 16),
        const _AdminTile(
          icon: Icons.settings_outlined,
          title: 'Настройки системы',
          path: '/admin/system-settings',
        ),
        const _AdminTile(
          icon: Icons.analytics_outlined,
          title: 'Аналитика',
          path: '/admin/analytics',
        ),
        const _AdminTile(
          icon: Icons.workspace_premium_outlined,
          title: 'Эффективность риелторов',
          path: '/admin/realtor-efficiency',
        ),
        const _AdminTile(
          icon: Icons.home_work_outlined,
          title: 'Недвижимость',
          path: '/admin/properties',
        ),
        const _AdminTile(
          icon: Icons.manage_search_outlined,
          title: 'Подбор недвижимости',
          path: '/admin/matching',
        ),
        const _AdminTile(
          icon: Icons.tune_outlined,
          title: 'Критерии недвижимости',
          path: '/admin/property-criteria',
        ),
        const _AdminTile(
          icon: Icons.payments_outlined,
          title: 'Валюты и курсы',
          path: '/admin/currencies',
        ),
        const _AdminTile(
          icon: Icons.people_outline,
          title: 'Клиенты',
          path: '/admin/clients',
        ),
        const _AdminTile(
          icon: Icons.admin_panel_settings_outlined,
          title: 'Управление доступом',
          path: '/admin/access',
        ),
        const _AdminTile(
          icon: Icons.description_outlined,
          title: 'Доступ к документам',
          path: '/admin/document-access',
        ),
        const _AdminTile(
          icon: Icons.person_outline,
          title: 'Аккаунт',
          path: '/admin/profile',
        ),
        const SizedBox(height: 8),
        FilledButton.tonalIcon(
          onPressed: () async {
            await ref.read(authControllerProvider.notifier).signOut();
            if (!context.mounted) {
              return;
            }
            context.go('/auth');
          },
          icon: const Icon(Icons.logout),
          label: const Text('Выйти'),
        ),
      ],
    );
  }
}

class _AdminTile extends StatelessWidget {
  const _AdminTile({
    required this.icon,
    required this.title,
    required this.path,
  });

  final IconData icon;
  final String title;
  final String path;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        leading: Icon(icon),
        title: Text(title, maxLines: 1, overflow: TextOverflow.ellipsis),
        trailing: const Icon(Icons.chevron_right),
        onTap: () => context.push(path),
      ),
    );
  }
}
