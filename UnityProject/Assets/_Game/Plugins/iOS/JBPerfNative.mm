// Device-health readings for the performance overlay and the device benchmark (ADR 0010).
// Called from C# through [DllImport("__Internal")] (JungleBooze.App.Perf.DeviceHealth). Read-only, no user data.
#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#include <mach/mach.h>
#include <os/proc.h>

extern "C"
{
    // NSProcessInfoThermalState: 0 nominal, 1 fair, 2 serious, 3 critical.
    int JBPerf_ThermalState()
    {
        return (int)[[NSProcessInfo processInfo] thermalState];
    }

    int JBPerf_LowPowerMode()
    {
        return [[NSProcessInfo processInfo] isLowPowerModeEnabled] ? 1 : 0;
    }

    // Physical footprint: the number iOS uses to terminate apps for memory (what Xcode's memory gauge shows).
    long long JBPerf_FootprintBytes()
    {
        task_vm_info_data_t info;
        mach_msg_type_number_t count = TASK_VM_INFO_COUNT;
        if (task_info(mach_task_self(), TASK_VM_INFO, (task_info_t)&info, &count) != KERN_SUCCESS)
        {
            return -1;
        }
        return (long long)info.phys_footprint;
    }

    // Memory the app can still allocate before the system terminates it (iOS 13+).
    long long JBPerf_AvailableBytes()
    {
        if (@available(iOS 13.0, *))
        {
            return (long long)os_proc_available_memory();
        }
        return -1;
    }
}
