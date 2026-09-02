import '../../../../core/auth/app_role.dart';
import 'deals_screen.dart';

class AdminDealsScreen extends DealsScreen {
  const AdminDealsScreen({super.key}) : super(role: AppRole.admin);
}
