import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/auth/auth_controller.dart';

class RealtorMoreScreen extends ConsumerWidget {
  const RealtorMoreScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('Еще', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 16),
        _MoreTile(
          icon: Icons.person_outline,
          title: 'Аккаунт',
          onTap: () => context.push('/realtor/profile'),
        ),
        _MoreTile(
          icon: Icons.insights_outlined,
          title: 'Моя эффективность',
          onTap: () => context.push('/realtor/efficiency'),
        ),
        _MoreTile(
          icon: Icons.report_outlined,
          title: 'Жалобы по мне',
          onTap: () => context.push('/realtor/complaints'),
        ),
        _MoreTile(
          icon: Icons.manage_search_outlined,
          title: 'Подбор недвижимости',
          onTap: () => context.push('/realtor/matching'),
        ),
        const _MoreTile(icon: Icons.groups_outlined, title: 'Риелторы'),
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

class _MoreTile extends StatelessWidget {
  const _MoreTile({required this.icon, required this.title, this.onTap});

  final IconData icon;
  final String title;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        leading: Icon(icon),
        title: Text(title, maxLines: 1, overflow: TextOverflow.ellipsis),
        trailing: onTap == null ? null : const Icon(Icons.chevron_right),
        onTap: onTap,
      ),
    );
  }
}
