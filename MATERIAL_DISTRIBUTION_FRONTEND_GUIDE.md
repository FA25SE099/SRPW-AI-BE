# Material Distribution - Frontend Implementation Guide

## Overview
This guide covers how to implement the Material Distribution feature for both **Web (Supervisor)** and **Mobile (Farmer)** applications.

---

## 🌐 Web Application (Supervisor)

### **1. Material Distribution Dashboard**

#### **Location**: `/groups/{groupId}/materials` or `/supervisor/material-distributions`

#### **UI Components**:

```tsx
// MaterialDistributionDashboard.tsx
const MaterialDistributionDashboard = ({ groupId }) => {
  const [distributions, setDistributions] = useState<MaterialDistributionsResponse>();
  const [filter, setFilter] = useState<'all' | 'pending' | 'overdue'>('all');

  useEffect(() => {
    fetchDistributions();
  }, [groupId]);

  const fetchDistributions = async () => {
    const response = await api.get(`/api/material-distribution/group/${groupId}`);
    setDistributions(response.data.data);
  };

  return (
    <div className="material-distribution-dashboard">
      {/* Summary Cards */}
      <div className="summary-cards">
        <StatCard
          title="Total Distributions"
          value={distributions?.totalDistributions}
          icon="📦"
        />
        <StatCard
          title="Pending"
          value={distributions?.pendingCount}
          status="warning"
          icon="⏳"
        />
        <StatCard
          title="Awaiting Farmer"
          value={distributions?.partiallyConfirmedCount}
          status="info"
          icon="👨‍🌾"
        />
        <StatCard
          title="Completed"
          value={distributions?.completedCount}
          status="success"
          icon="✅"
        />
      </div>

      {/* Filters */}
      <div className="filters">
        <button onClick={() => setFilter('all')}>All</button>
        <button onClick={() => setFilter('pending')}>Pending</button>
        <button onClick={() => setFilter('overdue')}>Overdue</button>
      </div>

      {/* Distribution List */}
      <div className="distribution-list">
        {distributions?.distributions
          .filter(d => filterDistributions(d, filter))
          .map(distribution => (
            <DistributionCard
              key={distribution.id}
              distribution={distribution}
              onConfirm={handleConfirm}
            />
          ))}
      </div>
    </div>
  );
};
```

#### **Distribution Card Component**:

```tsx
const DistributionCard = ({ distribution, onConfirm }) => {
  const isOverdue = distribution.isSupervisorOverdue;
  const daysUntilDeadline = calculateDaysUntil(distribution.supervisorConfirmationDeadline);

  return (
    <div className={`distribution-card ${isOverdue ? 'overdue' : ''}`}>
      {/* Header */}
      <div className="card-header">
        <div className="farmer-info">
          <h3>{distribution.farmerName}</h3>
          <span className="plot-name">{distribution.plotName}</span>
          <span className="phone">{distribution.farmerPhone}</span>
        </div>
        <StatusBadge status={distribution.status} />
      </div>

      {/* Material Info */}
      <div className="material-info">
        <div className="material-name">
          <span className="icon">🧪</span>
          <strong>{distribution.materialName}</strong>
        </div>
        <div className="quantity">
          {distribution.quantity} {distribution.unit}
        </div>
      </div>

      {/* Timeline */}
      <div className="timeline">
        <TimelineItem
          label="Scheduled"
          date={distribution.scheduledDistributionDate}
          status="past"
        />
        <TimelineItem
          label="Deadline"
          date={distribution.distributionDeadline}
          status={isOverdue ? 'overdue' : 'upcoming'}
          highlight={true}
        />
        <TimelineItem
          label="Confirm By"
          date={distribution.supervisorConfirmationDeadline}
          status={daysUntilDeadline < 0 ? 'overdue' : 'upcoming'}
        />
      </div>

      {/* Actions */}
      <div className="card-actions">
        {distribution.status === 'Pending' && (
          <button
            className="btn-primary"
            onClick={() => onConfirm(distribution.id)}
          >
            Confirm Distribution
          </button>
        )}
        {distribution.status === 'PartiallyConfirmed' && (
          <div className="waiting-farmer">
            <span>⏳ Waiting for farmer confirmation</span>
            <small>Farmer has until {formatDate(distribution.farmerConfirmationDeadline)}</small>
          </div>
        )}
        {distribution.status === 'Completed' && (
          <div className="completed-info">
            <span>✅ Completed</span>
            <small>
              Distributed: {formatDate(distribution.actualDistributionDate)}
              <br />
              Confirmed by farmer: {formatDate(distribution.farmerConfirmedAt)}
            </small>
          </div>
        )}
      </div>

      {/* Overdue Warning */}
      {isOverdue && (
        <div className="overdue-warning">
          ⚠️ Confirmation overdue by {Math.abs(daysUntilDeadline)} days!
        </div>
      )}
    </div>
  );
};
```

### **2. Confirm Distribution Modal**

```tsx
const ConfirmDistributionModal = ({ distributionId, onClose, onSuccess }) => {
  const [actualDate, setActualDate] = useState(new Date());
  const [notes, setNotes] = useState('');
  const [images, setImages] = useState<File[]>([]);
  const [uploading, setUploading] = useState(false);

  const handleSubmit = async () => {
    setUploading(true);
    
    try {
      // 1. Upload images first
      const imageUrls = await uploadImages(images);
      
      // 2. Confirm distribution
      const response = await api.post('/api/material-distribution/confirm', {
        materialDistributionId: distributionId,
        supervisorId: currentUser.id,
        actualDistributionDate: actualDate.toISOString(),
        notes: notes,
        imageUrls: imageUrls
      });

      if (response.data.succeeded) {
        toast.success('Distribution confirmed successfully!');
        onSuccess();
        onClose();
      } else {
        toast.error(response.data.errors.join(', '));
      }
    } catch (error) {
      toast.error('Failed to confirm distribution');
    } finally {
      setUploading(false);
    }
  };

  return (
    <Modal isOpen onClose={onClose} title="Confirm Material Distribution">
      <form onSubmit={(e) => { e.preventDefault(); handleSubmit(); }}>
        {/* Date Picker */}
        <div className="form-group">
          <label>Actual Distribution Date *</label>
          <DatePicker
            selected={actualDate}
            onChange={setActualDate}
            maxDate={new Date()}
            dateFormat="yyyy-MM-dd"
            required
          />
          <small>When did you actually distribute the materials?</small>
        </div>

        {/* Image Upload */}
        <div className="form-group">
          <label>Proof of Distribution (Photos) *</label>
          <ImageUploader
            multiple
            maxFiles={5}
            onFilesSelected={setImages}
            accept="image/*"
          />
          <small>Upload photos showing materials delivered (required)</small>
          
          {/* Image Preview */}
          <div className="image-preview">
            {images.map((file, index) => (
              <div key={index} className="preview-item">
                <img src={URL.createObjectURL(file)} alt={`Preview ${index}`} />
                <button
                  type="button"
                  onClick={() => setImages(images.filter((_, i) => i !== index))}
                >
                  ✕
                </button>
              </div>
            ))}
          </div>
        </div>

        {/* Notes */}
        <div className="form-group">
          <label>Notes (Optional)</label>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="Any additional notes about the distribution..."
            rows={4}
            maxLength={500}
          />
          <small>{notes.length}/500 characters</small>
        </div>

        {/* Actions */}
        <div className="modal-actions">
          <button type="button" onClick={onClose} disabled={uploading}>
            Cancel
          </button>
          <button
            type="submit"
            className="btn-primary"
            disabled={uploading || images.length === 0}
          >
            {uploading ? 'Uploading...' : 'Confirm Distribution'}
          </button>
        </div>
      </form>
    </Modal>
  );
};
```

### **3. Initiate Material Distribution (After Plan Approval)**

```tsx
// ProductionPlanDetailPage.tsx
const ProductionPlanDetailPage = ({ planId }) => {
  const [plan, setPlan] = useState<ProductionPlan>();
  const [showInitiateModal, setShowInitiateModal] = useState(false);

  const canInitiateDistribution = () => {
    return plan?.status === 'Approved' && !plan?.hasDistributions;
  };

  return (
    <div className="production-plan-detail">
      {/* Plan Info */}
      <div className="plan-header">
        <h1>{plan?.planName}</h1>
        <StatusBadge status={plan?.status} />
      </div>

      {/* Action Button */}
      {canInitiateDistribution() && (
        <div className="action-banner">
          <div className="banner-content">
            <span className="icon">📦</span>
            <div>
              <strong>Ready to distribute materials</strong>
              <p>Plan is approved. You can now initiate material distribution.</p>
            </div>
          </div>
          <button
            className="btn-primary"
            onClick={() => setShowInitiateModal(true)}
          >
            Initiate Material Distribution
          </button>
        </div>
      )}

      {/* Tasks List with Materials */}
      <div className="tasks-list">
        {plan?.tasks.map(task => (
          <TaskCard key={task.id} task={task} />
        ))}
      </div>

      {/* Modal */}
      {showInitiateModal && (
        <InitiateDistributionModal
          plan={plan}
          onClose={() => setShowInitiateModal(false)}
          onSuccess={() => {
            toast.success('Material distributions initiated!');
            navigate(`/groups/${plan.groupId}/materials`);
          }}
        />
      )}
    </div>
  );
};
```

### **4. Notifications & Alerts**

```tsx
const MaterialDistributionNotifications = () => {
  const [notifications, setNotifications] = useState([]);

  useEffect(() => {
    checkOverdueDistributions();
    const interval = setInterval(checkOverdueDistributions, 60000); // Check every minute
    return () => clearInterval(interval);
  }, []);

  const checkOverdueDistributions = async () => {
    const response = await api.get('/api/material-distribution/supervisor/pending');
    const overdue = response.data.data.filter(d => d.isSupervisorOverdue);
    
    if (overdue.length > 0) {
      showNotification({
        title: 'Material Distributions Overdue',
        message: `You have ${overdue.length} overdue material distributions`,
        type: 'error',
        action: () => navigate('/supervisor/material-distributions?filter=overdue')
      });
    }
  };

  return null; // Background service
};
```

---

## 📱 Mobile Application (Farmer)

### **1. Material Receipt Screen**

#### **Location**: Home Screen Widget or Notifications Tab

```tsx
// MaterialReceiptScreen.tsx
const MaterialReceiptScreen = () => {
  const [pendingReceipts, setPendingReceipts] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchPendingReceipts();
  }, []);

  const fetchPendingReceipts = async () => {
    try {
      const response = await api.get('/api/material-distribution/farmer/pending');
      setPendingReceipts(response.data.data);
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <LoadingSpinner />;

  if (pendingReceipts.length === 0) {
    return (
      <EmptyState
        icon="✅"
        title="All Caught Up!"
        message="No pending material confirmations"
      />
    );
  }

  return (
    <ScrollView style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.title}>Confirm Material Receipt</Text>
        <Text style={styles.subtitle}>
          {pendingReceipts.length} material{pendingReceipts.length > 1 ? 's' : ''} waiting for confirmation
        </Text>
      </View>

      {/* Pending Receipts */}
      {pendingReceipts.map(receipt => (
        <MaterialReceiptCard
          key={receipt.id}
          receipt={receipt}
          onConfirm={() => handleConfirm(receipt.id)}
        />
      ))}
    </ScrollView>
  );
};
```

### **2. Material Receipt Card (Mobile)**

```tsx
const MaterialReceiptCard = ({ receipt, onConfirm }) => {
  const daysRemaining = calculateDaysUntil(receipt.farmerConfirmationDeadline);
  const isUrgent = daysRemaining <= 1;
  const isOverdue = daysRemaining < 0;

  return (
    <TouchableOpacity
      style={[
        styles.card,
        isOverdue && styles.cardOverdue,
        isUrgent && styles.cardUrgent
      ]}
      onPress={() => onConfirm()}
    >
      {/* Urgency Banner */}
      {isOverdue && (
        <View style={styles.overdueBar}>
          <Text style={styles.overdueText}>
            ⚠️ Overdue by {Math.abs(daysRemaining)} day(s)
          </Text>
        </View>
      )}
      {isUrgent && !isOverdue && (
        <View style={styles.urgentBar}>
          <Text style={styles.urgentText}>
            ⏰ Confirm today!
          </Text>
        </View>
      )}

      {/* Material Info */}
      <View style={styles.cardContent}>
        <View style={styles.materialHeader}>
          <Text style={styles.materialIcon}>🧪</Text>
          <View style={styles.materialInfo}>
            <Text style={styles.materialName}>{receipt.materialName}</Text>
            <Text style={styles.quantity}>
              {receipt.quantity} {receipt.unit}
            </Text>
          </View>
        </View>

        {/* Plot Info */}
        <View style={styles.plotInfo}>
          <Text style={styles.label}>Plot:</Text>
          <Text style={styles.value}>{receipt.plotName}</Text>
        </View>

        {/* Distribution Info */}
        <View style={styles.distributionInfo}>
          <View style={styles.infoRow}>
            <Text style={styles.label}>Distributed by:</Text>
            <Text style={styles.value}>{receipt.supervisorName}</Text>
          </View>
          <View style={styles.infoRow}>
            <Text style={styles.label}>Date:</Text>
            <Text style={styles.value}>
              {formatDate(receipt.actualDistributionDate)}
            </Text>
          </View>
          <View style={styles.infoRow}>
            <Text style={styles.label}>Confirm by:</Text>
            <Text style={[
              styles.value,
              isOverdue && styles.overdueText,
              isUrgent && styles.urgentText
            ]}>
              {formatDate(receipt.farmerConfirmationDeadline)}
            </Text>
          </View>
        </View>

        {/* Supervisor Notes */}
        {receipt.supervisorNotes && (
          <View style={styles.notes}>
            <Text style={styles.notesLabel}>Supervisor's Note:</Text>
            <Text style={styles.notesText}>{receipt.supervisorNotes}</Text>
          </View>
        )}

        {/* Photos */}
        {receipt.imageUrls && receipt.imageUrls.length > 0 && (
          <View style={styles.photos}>
            <Text style={styles.photosLabel}>Delivery Photos:</Text>
            <ScrollView horizontal showsHorizontalScrollIndicator={false}>
              {receipt.imageUrls.map((url, index) => (
                <TouchableOpacity
                  key={index}
                  onPress={() => openImageViewer(url)}
                >
                  <Image
                    source={{ uri: url }}
                    style={styles.thumbnail}
                  />
                </TouchableOpacity>
              ))}
            </ScrollView>
          </View>
        )}

        {/* Confirm Button */}
        <TouchableOpacity
          style={styles.confirmButton}
          onPress={() => onConfirm()}
        >
          <Text style={styles.confirmButtonText}>
            ✓ Confirm Receipt
          </Text>
        </TouchableOpacity>
      </View>
    </TouchableOpacity>
  );
};
```

### **3. Confirmation Modal (Mobile)**

```tsx
const ConfirmReceiptModal = ({ receipt, visible, onClose, onSuccess }) => {
  const [notes, setNotes] = useState('');
  const [confirming, setConfirming] = useState(false);

  const handleConfirm = async () => {
    setConfirming(true);
    
    try {
      const response = await api.post('/api/material-distribution/confirm-receipt', {
        materialDistributionId: receipt.id,
        farmerId: currentUser.id,
        notes: notes
      });

      if (response.data.succeeded) {
        Alert.alert(
          'Success',
          'Material receipt confirmed!',
          [{ text: 'OK', onPress: () => { onSuccess(); onClose(); } }]
        );
      } else {
        Alert.alert('Error', response.data.errors.join(', '));
      }
    } catch (error) {
      Alert.alert('Error', 'Failed to confirm receipt');
    } finally {
      setConfirming(false);
    }
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent={true}
      onRequestClose={onClose}
    >
      <View style={styles.modalOverlay}>
        <View style={styles.modalContent}>
          {/* Header */}
          <View style={styles.modalHeader}>
            <Text style={styles.modalTitle}>Confirm Material Receipt</Text>
            <TouchableOpacity onPress={onClose}>
              <Text style={styles.closeButton}>✕</Text>
            </TouchableOpacity>
          </View>

          {/* Material Summary */}
          <View style={styles.summary}>
            <Text style={styles.summaryTitle}>{receipt.materialName}</Text>
            <Text style={styles.summaryQuantity}>
              {receipt.quantity} {receipt.unit}
            </Text>
            <Text style={styles.summaryPlot}>For: {receipt.plotName}</Text>
          </View>

          {/* Confirmation Checklist */}
          <View style={styles.checklist}>
            <Text style={styles.checklistTitle}>Please confirm:</Text>
            <View style={styles.checklistItem}>
              <Text style={styles.checkIcon}>✓</Text>
              <Text style={styles.checkText}>
                I received the materials shown above
              </Text>
            </View>
            <View style={styles.checklistItem}>
              <Text style={styles.checkIcon}>✓</Text>
              <Text style={styles.checkText}>
                The quantity matches what was delivered
              </Text>
            </View>
            <View style={styles.checklistItem}>
              <Text style={styles.checkIcon}>✓</Text>
              <Text style={styles.checkText}>
                Materials are in good condition
              </Text>
            </View>
          </View>

          {/* Notes Input */}
          <View style={styles.notesSection}>
            <Text style={styles.notesLabel}>
              Additional Notes (Optional)
            </Text>
            <TextInput
              style={styles.notesInput}
              multiline
              numberOfLines={4}
              placeholder="Any comments about the materials received..."
              value={notes}
              onChangeText={setNotes}
              maxLength={500}
            />
            <Text style={styles.charCount}>{notes.length}/500</Text>
          </View>

          {/* Actions */}
          <View style={styles.modalActions}>
            <TouchableOpacity
              style={styles.cancelButton}
              onPress={onClose}
              disabled={confirming}
            >
              <Text style={styles.cancelButtonText}>Cancel</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[
                styles.confirmButton,
                confirming && styles.confirmButtonDisabled
              ]}
              onPress={handleConfirm}
              disabled={confirming}
            >
              <Text style={styles.confirmButtonText}>
                {confirming ? 'Confirming...' : 'Confirm Receipt'}
              </Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
};
```

### **4. Push Notifications (Mobile)**

```tsx
// NotificationService.ts
export const setupMaterialDistributionNotifications = () => {
  // Listen for new material distributions
  messaging().onMessage(async remoteMessage => {
    if (remoteMessage.data?.type === 'MATERIAL_DISTRIBUTION') {
      const { materialName, quantity, deadline } = remoteMessage.data;
      
      // Show local notification
      await notifee.displayNotification({
        title: '📦 New Material Delivered',
        body: `${materialName} (${quantity}) - Confirm by ${deadline}`,
        android: {
          channelId: 'material-distribution',
          importance: AndroidImportance.HIGH,
          pressAction: {
            id: 'default',
            launchActivity: 'default',
          },
        },
        ios: {
          sound: 'default',
          critical: true,
        },
      });
    }
  });

  // Schedule daily reminder for pending confirmations
  scheduleDaily Reminder();
};

const scheduleDailyReminder = async () => {
  const trigger = {
    type: TriggerType.TIMESTAMP,
    timestamp: getTomorrowAt9AM(),
    repeatFrequency: RepeatFrequency.DAILY,
  };

  await notifee.createTriggerNotification(
    {
      title: '⏰ Pending Material Confirmations',
      body: 'You have materials waiting for confirmation',
      android: {
        channelId: 'material-reminder',
      },
    },
    trigger
  );
};
```

### **5. Home Screen Widget (Mobile)**

```tsx
const MaterialDistributionWidget = () => {
  const [pendingCount, setPendingCount] = useState(0);
  const [overdueCount, setOverdueCount] = useState(0);

  useEffect(() => {
    fetchPendingCount();
  }, []);

  if (pendingCount === 0) return null;

  return (
    <TouchableOpacity
      style={styles.widget}
      onPress={() => navigate('MaterialReceipts')}
    >
      <View style={styles.widgetContent}>
        <View style={styles.widgetIcon}>
          <Text style={styles.iconText}>📦</Text>
          {overdueCount > 0 && (
            <View style={styles.badge}>
              <Text style={styles.badgeText}>{overdueCount}</Text>
            </View>
          )}
        </View>
        <View style={styles.widgetText}>
          <Text style={styles.widgetTitle}>Material Confirmations</Text>
          <Text style={styles.widgetSubtitle}>
            {pendingCount} pending
            {overdueCount > 0 && ` (${overdueCount} overdue)`}
          </Text>
        </View>
        <Text style={styles.widgetArrow}>›</Text>
      </View>
    </TouchableOpacity>
  );
};
```

---

## 🔔 Notification Strategy

### **Web (Supervisor)**

```typescript
// Notification triggers
const supervisorNotifications = {
  // Daily at 8 AM
  dailyReminder: {
    time: '08:00',
    condition: 'hasPendingDistributions',
    message: 'You have {count} materials to distribute today'
  },
  
  // 1 day before deadline
  upcomingDeadline: {
    trigger: 'deadline - 1 day',
    message: 'Material distribution deadline tomorrow for {farmerName}'
  },
  
  // On deadline day
  deadlineToday: {
    trigger: 'deadline day',
    priority: 'high',
    message: 'Material distribution deadline TODAY for {farmerName}'
  },
  
  // Overdue
  overdue: {
    trigger: 'deadline + 1 day',
    priority: 'urgent',
    message: 'OVERDUE: Material distribution for {farmerName}'
  }
};
```

### **Mobile (Farmer)**

```typescript
// Notification triggers
const farmerNotifications = {
  // When supervisor confirms
  materialDelivered: {
    trigger: 'supervisor_confirmed',
    priority: 'high',
    message: '{materialName} delivered - Please confirm receipt'
  },
  
  // Daily reminder if pending
  dailyReminder: {
    time: '18:00', // Evening reminder
    condition: 'hasPendingReceipts',
    message: 'Don\'t forget to confirm material receipt'
  },
  
  // 1 day before deadline
  upcomingDeadline: {
    trigger: 'deadline - 1 day',
    priority: 'high',
    message: 'Confirm material receipt by tomorrow'
  },
  
  // Overdue
  overdue: {
    trigger: 'deadline + 1 day',
    priority: 'urgent',
    message: 'URGENT: Material confirmation overdue'
  }
};
```

---

## 📊 API Integration

### **API Endpoints**:

```typescript
// Material Distribution API
const materialDistributionAPI = {
  // Get distributions for group
  getForGroup: (groupId: string) =>
    api.get(`/api/material-distribution/group/${groupId}`),
  
  // Get pending for supervisor
  getPendingForSupervisor: (supervisorId: string) =>
    api.get(`/api/material-distribution/supervisor/${supervisorId}/pending`),
  
  // Get pending for farmer
  getPendingForFarmer: (farmerId: string) =>
    api.get(`/api/material-distribution/farmer/${farmerId}/pending`),
  
  // Initiate distribution
  initiate: (data: InitiateDistributionRequest) =>
    api.post('/api/material-distribution/initiate', data),
  
  // Supervisor confirms
  confirm: (data: ConfirmDistributionRequest) =>
    api.post('/api/material-distribution/confirm', data),
  
  // Farmer confirms receipt
  confirmReceipt: (data: ConfirmReceiptRequest) =>
    api.post('/api/material-distribution/confirm-receipt', data),
};
```

---

## 🎨 UI/UX Best Practices

### **Color Coding**:
```css
.status-pending { background: #FFF3CD; color: #856404; }
.status-partially-confirmed { background: #D1ECF1; color: #0C5460; }
.status-completed { background: #D4EDDA; color: #155724; }
.status-overdue { background: #F8D7DA; color: #721C24; }
```

### **Icons**:
- 📦 Material/Distribution
- ⏳ Pending
- ✅ Completed
- ⚠️ Overdue
- 👨‍🌾 Farmer
- 🧪 Chemical/Fertilizer
- 🌾 Seeds

### **Mobile Gestures**:
- Swipe right on card → Quick confirm
- Swipe left on card → View details
- Pull to refresh → Update list
- Long press → View photos full screen

---

## ✅ Testing Checklist

### **Web (Supervisor)**:
- [ ] View all distributions for a group
- [ ] Filter by status (pending/overdue/completed)
- [ ] Confirm distribution with photos
- [ ] See overdue warnings
- [ ] Receive notifications
- [ ] View farmer confirmation status

### **Mobile (Farmer)**:
- [ ] See pending material receipts
- [ ] View delivery photos
- [ ] Confirm receipt with notes
- [ ] Receive push notifications
- [ ] See overdue alerts
- [ ] View confirmation history

---

## 🚀 Implementation Priority

1. **Phase 1** (MVP):
   - Basic list view (web & mobile)
   - Confirm distribution (supervisor)
   - Confirm receipt (farmer)

2. **Phase 2**:
   - Overdue detection & alerts
   - Image upload & viewing
   - Notifications

3. **Phase 3**:
   - Advanced filtering
   - Analytics dashboard
   - Batch operations

---

**This guide provides everything your frontend team needs to implement the Material Distribution feature! 🌾**

