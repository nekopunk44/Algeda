import '../../../../core/auth/app_role.dart';
import 'chats_screen.dart';

class AdminChatsScreen extends ChatsScreen {
  const AdminChatsScreen({super.key}) : super(role: AppRole.admin);
}
