import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../core/auth/app_role.dart';
import '../features/notifications/data/local/mobile_notification_service.dart';
import '../features/notifications/data/repository/notifications_repository.dart';
import '../features/notifications/domain/models/notification_models.dart';

class RoleShell extends ConsumerStatefulWidget {
  const RoleShell({required this.role, required this.child, super.key});

  final AppRole role;
  final Widget child;

  @override
  ConsumerState<RoleShell> createState() => _RoleShellState();
}

class _RoleShellState extends ConsumerState<RoleShell>
    with WidgetsBindingObserver {
  Timer? _timer;
  List<AppNotification> _notifications = const [];
  final Set<String> _locallyDisplayed = {};
  AppLifecycleState _lifecycleState = AppLifecycleState.resumed;
  bool _isLoadingNotifications = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    unawaited(MobileNotificationService.instance.initialize());
    unawaited(_loadNotifications(showNativeWhenBackground: false));
    _timer = Timer.periodic(const Duration(seconds: 15), (_) {
      unawaited(
        _loadNotifications(
          showNativeWhenBackground:
              _lifecycleState != AppLifecycleState.resumed,
        ),
      );
    });
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _timer?.cancel();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    _lifecycleState = state;
    if (state == AppLifecycleState.resumed) {
      unawaited(_loadNotifications(showNativeWhenBackground: false));
    }
  }

  @override
  Widget build(BuildContext context) {
    final destinations = _destinationsFor(widget.role);
    final location = GoRouterState.of(context).uri.path;
    final currentIndex = _currentIndex(location, destinations);
    final canPopRoute = GoRouter.of(context).canPop();

    return PopScope(
      canPop: canPopRoute || currentIndex == 0,
      onPopInvokedWithResult: (didPop, _) {
        if (didPop || canPopRoute || currentIndex == 0) {
          return;
        }

        context.go(destinations.first.path);
      },
      child: Scaffold(
        appBar: AppBar(
          title: _BrandTitle(role: widget.role),
          actions: [
            _NotificationBell(
              unreadCount: _totalUnread,
              isLoading: _isLoadingNotifications,
              onPressed: _openNotifications,
            ),
          ],
        ),
        body: SafeArea(child: widget.child),
        bottomNavigationBar: NavigationBar(
          selectedIndex: currentIndex,
          onDestinationSelected: (index) {
            context.go(destinations[index].path);
          },
          destinations: [
            for (final item in destinations)
              NavigationDestination(
                icon: Icon(item.icon),
                selectedIcon: Icon(item.selectedIcon),
                label: item.label,
              ),
          ],
        ),
      ),
    );
  }

  int get _totalUnread {
    return _notifications.fold(0, (sum, item) => sum + item.unreadCount);
  }

  Future<void> _loadNotifications({
    required bool showNativeWhenBackground,
  }) async {
    if (_isLoadingNotifications) {
      return;
    }

    _isLoadingNotifications = true;
    try {
      final items = await ref.read(notificationsRepositoryProvider).getUnread();
      final previousIds = _notifications.map((item) => item.id).toSet();
      if (mounted) {
        setState(() => _notifications = items);
      }

      if (showNativeWhenBackground) {
        for (final item in items) {
          final signal =
              '${item.id}:${item.unreadCount}:${item.occurredAtUtc.toIso8601String()}';
          final isFreshSignal = _locallyDisplayed.add(signal);
          if (!previousIds.contains(item.id) || isFreshSignal) {
            await MobileNotificationService.instance.showChatNotification(item);
          }
        }
      }
    } catch (_) {
      // Веб-виджет тоже молча переживает временную недоступность уведомлений.
    } finally {
      _isLoadingNotifications = false;
      if (mounted) {
        setState(() {});
      }
    }
  }

  Future<void> _openNotifications() async {
    final selected = await showModalBottomSheet<AppNotification>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.zero),
      builder: (context) => _NotificationsSheet(items: _notifications),
    );

    await _markReadAndRefresh();

    if (!mounted || selected == null || selected.dealId.isEmpty) {
      return;
    }

    context.push('${widget.role.homePath}/chats/${selected.dealId}');
  }

  Future<void> _markReadAndRefresh() async {
    if (_notifications.isEmpty) {
      return;
    }
    try {
      await ref.read(notificationsRepositoryProvider).markAllRead();
    } catch (_) {
      // Если отметка не прошла, следующий polling снова покажет непрочитанное.
    }
    await _loadNotifications(showNativeWhenBackground: false);
  }

  static int _currentIndex(
    String location,
    List<_ShellDestination> destinations,
  ) {
    if (location.endsWith('/profile')) {
      return destinations.length - 1;
    }

    if (location.startsWith('/admin/properties')) {
      return destinations.length - 1;
    }

    if (location.startsWith('/admin/realtor-efficiency') ||
        location.startsWith('/admin/system-settings') ||
        location.startsWith('/admin/eligibility-settings') ||
        location.startsWith('/admin/analytics') ||
        location.startsWith('/admin/matching') ||
        location.startsWith('/realtor/efficiency') ||
        location.startsWith('/realtor/complaints') ||
        location.startsWith('/realtor/matching')) {
      return destinations.length - 1;
    }

    final index = destinations.indexWhere((item) => item.matches(location));
    return index < 0 ? 0 : index;
  }

  static List<_ShellDestination> _destinationsFor(AppRole role) {
    return switch (role) {
      AppRole.realtor => const [
        _ShellDestination(
          path: '/realtor',
          label: 'Главная',
          icon: Icons.dashboard_outlined,
          selectedIcon: Icons.dashboard,
        ),
        _ShellDestination(
          path: '/realtor/deals',
          label: 'Заявки',
          icon: Icons.assignment_outlined,
          selectedIcon: Icons.assignment,
        ),
        _ShellDestination(
          path: '/realtor/properties',
          label: 'Объекты',
          icon: Icons.home_work_outlined,
          selectedIcon: Icons.home_work,
        ),
        _ShellDestination(
          path: '/realtor/chats',
          label: 'Чаты',
          icon: Icons.chat_bubble_outline,
          selectedIcon: Icons.chat_bubble,
        ),
        _ShellDestination(
          path: '/realtor/more',
          label: 'Еще',
          icon: Icons.more_horiz,
          selectedIcon: Icons.more,
        ),
      ],
      AppRole.admin => const [
        _ShellDestination(
          path: '/admin',
          label: 'Главная',
          icon: Icons.dashboard_outlined,
          selectedIcon: Icons.dashboard,
        ),
        _ShellDestination(
          path: '/admin/deals',
          label: 'Заявки',
          icon: Icons.assignment_outlined,
          selectedIcon: Icons.assignment,
        ),
        _ShellDestination(
          path: '/admin/complaints',
          label: 'Жалобы',
          icon: Icons.report_problem_outlined,
          selectedIcon: Icons.report_problem,
        ),
        _ShellDestination(
          path: '/admin/chats',
          label: 'Чаты',
          icon: Icons.forum_outlined,
          selectedIcon: Icons.forum,
        ),
        _ShellDestination(
          path: '/admin/more',
          label: 'Еще',
          icon: Icons.more_horiz,
          selectedIcon: Icons.more,
        ),
      ],
    };
  }
}

class _NotificationBell extends StatelessWidget {
  const _NotificationBell({
    required this.unreadCount,
    required this.isLoading,
    required this.onPressed,
  });

  final int unreadCount;
  final bool isLoading;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(right: 8),
      child: Badge(
        isLabelVisible: unreadCount > 0,
        label: Text(unreadCount > 99 ? '99+' : unreadCount.toString()),
        child: IconButton(
          onPressed: onPressed,
          icon: isLoading && unreadCount == 0
              ? const SizedBox.square(
                  dimension: 20,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.notifications_outlined),
          tooltip: 'Уведомления',
        ),
      ),
    );
  }
}

class _NotificationsSheet extends StatelessWidget {
  const _NotificationsSheet({required this.items});

  final List<AppNotification> items;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(
                  Icons.notifications_outlined,
                  color: Theme.of(context).colorScheme.primary,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Уведомления',
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                ),
                IconButton(
                  onPressed: () => Navigator.of(context).pop(),
                  icon: const Icon(Icons.close),
                  tooltip: 'Закрыть',
                ),
              ],
            ),
            const SizedBox(height: 8),
            if (items.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 24),
                child: Center(child: Text('Пока уведомлений нет.')),
              )
            else
              ConstrainedBox(
                constraints: BoxConstraints(
                  maxHeight: MediaQuery.sizeOf(context).height * 0.62,
                ),
                child: ListView.separated(
                  shrinkWrap: true,
                  itemCount: items.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 8),
                  itemBuilder: (context, index) {
                    final item = items[index];
                    return _NotificationTile(
                      item: item,
                      onTap: () => Navigator.of(context).pop(item),
                    );
                  },
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _NotificationTile extends StatelessWidget {
  const _NotificationTile({required this.item, required this.onTap});

  final AppNotification item;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final date = DateFormat(
      'dd.MM.yyyy HH:mm',
    ).format(item.occurredAtUtc.toLocal());

    return Material(
      color: item.isUnread
          ? colors.primaryContainer.withValues(alpha: 0.45)
          : colors.surface,
      borderRadius: BorderRadius.circular(8),
      child: InkWell(
        borderRadius: BorderRadius.circular(8),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Text(
                      item.title,
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                  ),
                  if (item.unreadCount > 1)
                    Badge(label: Text(item.unreadCount.toString())),
                ],
              ),
              const SizedBox(height: 4),
              Text(item.message),
              const SizedBox(height: 8),
              Text(date, style: Theme.of(context).textTheme.bodySmall),
            ],
          ),
        ),
      ),
    );
  }
}

class _BrandTitle extends StatelessWidget {
  const _BrandTitle({required this.role});

  final AppRole role;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final textTheme = Theme.of(context).textTheme;

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.house_rounded, color: colors.primary, size: 22),
        const SizedBox(width: 8),
        Text('Real Estate', style: textTheme.titleMedium),
        const SizedBox(width: 8),
        Flexible(
          child: Text(
            role.title,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: textTheme.bodySmall,
          ),
        ),
      ],
    );
  }
}

class _ShellDestination {
  const _ShellDestination({
    required this.path,
    required this.label,
    required this.icon,
    required this.selectedIcon,
  });

  final String path;
  final String label;
  final IconData icon;
  final IconData selectedIcon;

  bool matches(String location) {
    if (location == path) {
      return true;
    }

    return path != '/realtor' &&
        path != '/admin' &&
        location.startsWith('$path/');
  }
}
